using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Npgsql.Pipelines;

class ObjectPool<T>
{
    // Modest number to re-use. We only really care about reuse for short lived scopes
    public const int DefaultMaxPoolSize = 128;

    int _count;
    readonly ConcurrentQueue<T> _queue = new();
    readonly Func<T> _factory;
    readonly int _maxPoolSize;

    public ObjectPool(Func<ObjectPool<T>, Func<T>> activator, int maxPoolSize = DefaultMaxPoolSize)
    {
        _maxPoolSize = maxPoolSize;
        _factory = activator(this);
    }

    public T Rent()
    {
        if (_queue.TryDequeue(out var state))
        {
            Interlocked.Decrement(ref _count);
            return state;
        }
        return _factory();
    }

    public void Return(T value)
    {
        if (Interlocked.Increment(ref _count) > _maxPoolSize)
        {
            Interlocked.Decrement(ref _count);
        }

        _queue.Enqueue(value);
    }
}
