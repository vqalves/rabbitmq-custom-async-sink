using RabbitMQ.Client;

namespace ApiWithLog.Logging;

public class RabbitMqBufferSynchronizer: BackgroundService
{
    private readonly SyncToAsyncBufferQueue _queue;
    private readonly RabbitMqConfiguration _rabbitMqConfiguration;
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private bool _disposed;

    private RabbitMqBufferSynchronizer(
        IConnection connection,
        IChannel channel,
        SyncToAsyncBufferQueue queue,
        RabbitMqConfiguration rabbitMqConfiguration)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _rabbitMqConfiguration = rabbitMqConfiguration ?? throw new ArgumentNullException(nameof(rabbitMqConfiguration));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine($"{nameof(RabbitMqBufferSynchronizer)}.{nameof(ExecuteAsync)}: BackgroundService started");

        var properties = new BasicProperties
        {
            Persistent = true
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (_queue.TryDequeue(out var binaryMessage))
                {
                    try
                    {
                        await _channel.BasicPublishAsync(
                            exchange: string.Empty,
                            routingKey: _rabbitMqConfiguration.QueueName,
                            mandatory: false,
                            basicProperties: properties,
                            body: binaryMessage,
                            cancellationToken: stoppingToken);
                    }
                    catch
                    {
                        // Don't stop worker in case of error
                        Console.Error.WriteLine($"{nameof(RabbitMqBufferSynchronizer)}.{nameof(ExecuteAsync)}: Failed to publish message. Message will be dropped.");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{nameof(RabbitMqBufferSynchronizer)}.{nameof(ExecuteAsync)} - Exception thrown: {ex.Message}");
            }
        }

        Console.WriteLine($"{nameof(RabbitMqBufferSynchronizer)}.{nameof(ExecuteAsync)}: BackgroundService ended");
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            base.Dispose();
            return;
        }

        try { _channel?.Dispose(); }
        catch { }

        try { _connection?.Dispose(); }
        catch { }

        _disposed = true;
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public static async Task<RabbitMqBufferSynchronizer> CreateNewAsync(
        SyncToAsyncBufferQueue queue,
        RabbitMqConfiguration rabbitMqConfiguration)
    {
        var factory = new ConnectionFactory
        {
            HostName = rabbitMqConfiguration.HostName,
            Port = rabbitMqConfiguration.Port,
            UserName = rabbitMqConfiguration.UserName,
            Password = rabbitMqConfiguration.Password
        };

        IConnection? connection = null;
        IChannel? channel = null;

        try
        {
            connection = await factory.CreateConnectionAsync();
            channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: rabbitMqConfiguration.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            return new RabbitMqBufferSynchronizer(connection, channel, queue, rabbitMqConfiguration);
        }
        catch
        {
            try { channel?.Dispose(); }
            catch { /* Ignore disposal errors during rollback */ }

            try { connection?.Dispose(); }
            catch { /* Ignore disposal errors during rollback */ }

            throw;
        }
    }
}