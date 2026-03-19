using System.Collections.Concurrent;
using ILogger = Serilog.ILogger;

namespace ApiWithLog.Logging;

public class SyncToAsyncBufferQueue
{
    private readonly int _bufferMaxSize;
    private readonly ConcurrentQueue<byte[]> _queue;
    private readonly ILogger _logger;
    private int _currentCount;

    public SyncToAsyncBufferQueue(int bufferMaxSize, ILogger logger)
    {
        if (bufferMaxSize <= 0)
            throw new ArgumentException("Buffer max size must be greater than zero.", nameof(bufferMaxSize));

        _bufferMaxSize = bufferMaxSize;
        _queue = new ConcurrentQueue<byte[]>();
        _currentCount = 0;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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