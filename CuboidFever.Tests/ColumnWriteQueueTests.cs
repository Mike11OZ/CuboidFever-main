using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class ColumnWriteQueueTests
{
    // An in-memory store that can be told to stall or fail, to exercise the queue's timing and retry rules
    private sealed class MemoryStore : IColumnStore
    {
        private readonly Dictionary<(int, int), byte[]> _data = new();
        private readonly object _lock = new();
        private int _writeCalls;

        public ManualResetEventSlim Gate;   // when set, Write waits here until it is signalled
        public int FailFirst;               // the first this-many Write calls throw
        public bool AlwaysFail;

        public int WriteCalls => Volatile.Read(ref _writeCalls);

        public byte[] Read(int cx, int cz)
        {
            lock (_lock) return _data.TryGetValue((cx, cz), out var data) ? data : null;
        }

        public void Write(int cx, int cz, byte[] data)
        {
            Gate?.Wait(TimeSpan.FromSeconds(10));

            int call = Interlocked.Increment(ref _writeCalls);
            if (AlwaysFail || call <= FailFirst) throw new IOException("disk full");

            lock (_lock) _data[(cx, cz)] = data;
        }

        public void Delete(int cx, int cz)
        {
            lock (_lock) _data.Remove((cx, cz));
        }

        public bool Exists(int cx, int cz)
        {
            lock (_lock) return _data.ContainsKey((cx, cz));
        }

        public IEnumerable<(int cx, int cz)> ListColumns()
        {
            lock (_lock)
            {
                var list = new List<(int cx, int cz)>();
                foreach (var key in _data.Keys) list.Add((key.Item1, key.Item2));
                return list;
            }
        }
    }

    private static ColumnWriteQueue Queue(MemoryStore store, int maxAttempts = 5) =>
        new(store, maxAttempts, TimeSpan.FromMilliseconds(1));

    [Fact]
    public void QueuedColumns_AreWrittenByFlush()
    {
        var store = new MemoryStore();
        using var queue = Queue(store);

        for (int i = 0; i < 20; i++)
            queue.Queue(i, -i, new[] { (byte)i, (byte)(i + 1) });

        Assert.True(queue.Flush());

        for (int i = 0; i < 20; i++)
            Assert.Equal(new[] { (byte)i, (byte)(i + 1) }, store.Read(i, -i));
    }

    [Fact]
    public void ReadThrough_SeesDataThatIsNotOnDiskYet()
    {
        var gate = new ManualResetEventSlim(false);
        var store = new MemoryStore { Gate = gate };
        using var queue = Queue(store);

        try
        {
            var data = new byte[] { 1, 2, 3 };
            queue.Queue(1, 1, data);

            // The write is stuck at the gate: the store has nothing, but a reload still gets the edit
            Assert.Null(store.Read(1, 1));
            Assert.Equal(data, queue.ReadThrough(1, 1));
        }
        finally
        {
            gate.Set();
        }

        Assert.True(queue.Flush());
        Assert.Equal(new byte[] { 1, 2, 3 }, store.Read(1, 1));
        Assert.Equal(new byte[] { 1, 2, 3 }, queue.ReadThrough(1, 1));
    }

    [Fact]
    public void ReadThrough_NeverSaved_IsNull()
    {
        using var queue = Queue(new MemoryStore());

        Assert.Null(queue.ReadThrough(5, 5));
    }

    [Fact]
    public void QueuingAColumnAgain_KeepsTheNewestBytes()
    {
        var gate = new ManualResetEventSlim(false);
        var store = new MemoryStore { Gate = gate };
        using var queue = Queue(store);

        try
        {
            queue.Queue(2, 2, new byte[] { 1 });
            queue.Queue(2, 2, new byte[] { 2 });
            queue.Queue(2, 2, new byte[] { 3 });

            Assert.Equal(new byte[] { 3 }, queue.ReadThrough(2, 2));
        }
        finally
        {
            gate.Set();
        }

        Assert.True(queue.Flush());
        Assert.Equal(new byte[] { 3 }, store.Read(2, 2));
    }

    [Fact]
    public void ManyThreadsQueuingTheSameColumns_EndWithTheNewestBytes()
    {
        var store = new MemoryStore();
        using var queue = Queue(store);

        var last = new byte[10][];
        var turn = new object();
        var threads = new List<Thread>();

        for (int t = 0; t < 4; t++)
        {
            int id = t;
            var thread = new Thread(() =>
            {
                for (int i = 0; i < 300; i++)
                {
                    int column = (i + id) % 10;
                    var data = new[] { (byte)id, (byte)(i & 255), (byte)(i >> 8) };

                    // Queue and record under one lock, so "newest" has a single answer
                    lock (turn)
                    {
                        queue.Queue(column, 0, data);
                        last[column] = data;
                    }
                }
            });

            threads.Add(thread);
            thread.Start();
        }

        foreach (var thread in threads) thread.Join();

        Assert.True(queue.Flush());

        for (int column = 0; column < 10; column++)
        {
            Assert.Equal(last[column], store.Read(column, 0));
            Assert.Equal(last[column], queue.ReadThrough(column, 0));
        }
    }

    [Fact]
    public void AFailedWrite_IsRetriedUntilItWorks()
    {
        var store = new MemoryStore { FailFirst = 2 };
        using var queue = Queue(store, maxAttempts: 5);

        queue.Queue(3, 3, new byte[] { 7 });

        Assert.True(queue.Flush());
        Assert.Equal(new byte[] { 7 }, store.Read(3, 3));
        Assert.Equal(3, store.WriteCalls);
    }

    [Fact]
    public void AWriteThatNeverWorks_MakesFlushReturnFalse_ButTheDataStaysReadable()
    {
        var store = new MemoryStore { AlwaysFail = true };
        using var queue = Queue(store, maxAttempts: 2);

        var data = new byte[] { 9, 9 };
        queue.Queue(4, 4, data);

        Assert.False(queue.Flush());
        Assert.Equal(data, queue.ReadThrough(4, 4));
        Assert.Null(store.Read(4, 4));
    }

    [Fact]
    public void Dispose_WritesEverythingQueued_AndRefusesNewWork()
    {
        var store = new MemoryStore();
        var queue = Queue(store);

        queue.Queue(3, 4, new byte[] { 9 });
        queue.Dispose();

        Assert.Equal(new byte[] { 9 }, store.Read(3, 4));
        Assert.Throws<ObjectDisposedException>(() => queue.Queue(1, 1, new byte[] { 1 }));

        queue.Dispose();   // a second Dispose is harmless
    }
}
