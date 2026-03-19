using System.Collections.Concurrent;

namespace ApiWithLog.Logging;

public class SyncToAsyncBufferQueue
{
    private readonly int _bufferMaxSize;
    private readonly ConcurrentQueue<byte[]> _queue;
    private int _currentCount;

    public SyncToAsyncBufferQueue(int bufferMaxSize)
    {
        if (bufferMaxSize <= 0)
            throw new ArgumentException("Buffer max size must be greater than zero.", nameof(bufferMaxSize));

        _bufferMaxSize = bufferMaxSize;
        _queue = new ConcurrentQueue<byte[]>();
        _currentCount = 0;
    }

    public void Enqueue(byte[] item)
    {
        // Thread-safe check: Increment and check if we're within the limit
        var newCount = Interlocked.Increment(ref _currentCount);

        if (newCount > _bufferMaxSize)
        {
            // Buffer is full, decrement back and drop the item
            Interlocked.Decrement(ref _currentCount);
            Console.Error.WriteLine($"{nameof(SyncToAsyncBufferQueue)}.{nameof(Enqueue)}: Message dropped because buffer is full ({_bufferMaxSize} items)");
            return;
        }

        // Add to queue
        _queue.Enqueue(item);
    }

    public bool TryDequeue(out byte[]? item)
    {
        if (_queue.TryDequeue(out var dequeuedItem))
        {
            // Successfully dequeued, decrement the count
            Interlocked.Decrement(ref _currentCount);
            item = dequeuedItem;
            return true;
        }

        // Queue is empty
        item = default;
        return false;
    }
}