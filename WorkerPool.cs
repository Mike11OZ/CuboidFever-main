using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CuboidFever;

public sealed class WorkerPool : IDisposable
{
    private readonly BlockingCollection<Action> _jobs = new(new ConcurrentQueue<Action>());
    private readonly Thread[] _threads;
    private readonly object _lifecycleLock = new();

    private bool _disposed;

    public int Count => _threads.Length;

    public WorkerPool(int threads)
    {
        if (threads <= 0)
            throw new ArgumentOutOfRangeException(nameof(threads));

        _threads = new Thread[threads];

        for (int i = 0; i < threads; i++)
        {
            _threads[i] = new Thread(Loop)
            {
                IsBackground = true,
                Name = $"CuboidFever worker {i}"
            };

            _threads[i].Start();
        }
    }

    public void Enqueue(Action job)
    {
        ArgumentNullException.ThrowIfNull(job);

        lock (_lifecycleLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WorkerPool));

            _jobs.Add(job);
        }
    }

    private void Loop()
    {
        foreach (var job in _jobs.GetConsumingEnumerable())
        {
            try
            {
                job();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[worker] {e}");
            }
        }
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _jobs.CompleteAdding();
        }

        foreach (var thread in _threads)
            thread.Join();

        _jobs.Dispose();
    }
}