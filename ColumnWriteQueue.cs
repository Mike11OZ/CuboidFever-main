using System;
using System.Collections.Generic;
using System.Threading;

namespace CuboidFever;

// Writes saved columns to an IColumnStore on a background thread, so saving never stalls a frame.
//
// The caller turns a column into bytes first (ColumnSerializer.Write, on the main thread, where the column's
// blocks can't change under it) and hands the bytes to Queue. From then on:
//
//   - ReadThrough returns queued bytes that have not reached the store yet. A column that is unloaded and
//     reloaded a moment later therefore comes back with its latest edits, not the older copy on disk.
//   - Queuing a column again before its earlier bytes were written replaces them: only the newest is kept.
//   - A write that fails is retried a few times, then kept in memory (still readable) and tried once more by
//     Flush. Flush returns false if something still could not be written.
//
// Everything is guarded by one lock. The lock is never held while touching the disk.
public sealed class ColumnWriteQueue : IDisposable
{
    private readonly IColumnStore _store;
    private readonly int _maxAttempts;
    private readonly TimeSpan _retryDelay;

    private readonly object _lock = new();
    private readonly Dictionary<(int, int), byte[]> _pending = new(); // newest bytes per column, until written
    private readonly Queue<(int, int)> _jobs = new();                 // columns waiting for the writer; may repeat
    private readonly Thread _thread;
    private int _outstanding;   // jobs queued and not yet finished (including the one being written)
    private bool _stopping;
    private bool _disposed;

    public ColumnWriteQueue(IColumnStore store, int maxAttempts = 5, TimeSpan? retryDelay = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _maxAttempts = Math.Max(1, maxAttempts);
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);

        _thread = new Thread(WriterLoop) { IsBackground = true, Name = "ColumnWriter" };
        _thread.Start();
    }

    // Takes ownership of 'data': the caller must not change the array afterwards.
    public void Queue(int cx, int cz, byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ColumnWriteQueue));

            _pending[(cx, cz)] = data;
            _jobs.Enqueue((cx, cz));
            _outstanding++;
            Monitor.PulseAll(_lock);
        }
    }

    // The newest saved bytes for a column: the queued copy if it has not been written yet, otherwise whatever
    // the store has (null if the column was never saved). Safe to call from any thread.
    public byte[] ReadThrough(int cx, int cz)
    {
        lock (_lock)
        {
            if (_pending.TryGetValue((cx, cz), out var data)) return data;
        }

        return _store.Read(cx, cz);
    }

    // Blocks until everything queued so far is on disk. Columns that failed all their retries earlier get one
    // more attempt here. Returns false if any column still could not be written.
    public bool Flush()
    {
        for (int round = 0; round < 2; round++)
        {
            lock (_lock)
            {
                while (_outstanding > 0) Monitor.Wait(_lock);

                if (_pending.Count == 0) return true;
                if (round == 1) return false;

                // Whatever is left failed every attempt: give each column one more go
                foreach (var key in _pending.Keys)
                {
                    _jobs.Enqueue(key);
                    _outstanding++;
                }
                Monitor.PulseAll(_lock);
            }
        }

        return false;
    }

    // Writes everything still queued, then stops the writer thread. Safe to call more than once.
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
        }

        Flush();

        lock (_lock)
        {
            _disposed = true;
            _stopping = true;
            Monitor.PulseAll(_lock);
        }

        _thread.Join(TimeSpan.FromSeconds(10));
    }

    private void WriterLoop()
    {
        while (true)
        {
            (int, int) key;
            byte[] data;

            lock (_lock)
            {
                while (_jobs.Count == 0 && !_stopping) Monitor.Wait(_lock);
                if (_jobs.Count == 0) return;   // stopping, and nothing left to write

                key = _jobs.Dequeue();
                _pending.TryGetValue(key, out data);   // the newest bytes, which may be newer than when queued
            }

            // data is null when an earlier job for this column already wrote the newest bytes
            bool written = data == null || TryWrite(key, data);

            lock (_lock)
            {
                // Forget the bytes only if they are still the newest: a newer Queue may have replaced them
                // while this write was running, and that newer copy has a job of its own waiting.
                if (written && data != null &&
                    _pending.TryGetValue(key, out var current) && ReferenceEquals(current, data))
                {
                    _pending.Remove(key);
                }

                _outstanding--;
                Monitor.PulseAll(_lock);
            }
        }
    }

    private bool TryWrite((int cx, int cz) key, byte[] data)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                _store.Write(key.cx, key.cz, data);
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"[Save] Writing column ({key.cx}, {key.cz}) failed (attempt {attempt} of {_maxAttempts}): {e.Message}");

                if (attempt >= _maxAttempts) return false;
            }

            Thread.Sleep(_retryDelay);
        }
    }
}
