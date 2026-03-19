using RabbitMQ.Client;
using Serilog.Context;
using ILogger = Serilog.ILogger;

namespace ApiWithLog.Logging;

public class RabbitMqBufferSynchronizer: BackgroundService
{
    private readonly SyncToAsyncBufferQueue _queue;
    private readonly RabbitMqConfiguration _rabbitMqConfiguration;
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger _logger;
    private bool _disposed;

    private RabbitMqBufferSynchronizer(
        IConnection connection,
        IChannel channel,
        SyncToAsyncBufferQueue queue,
        RabbitMqConfiguration rabbitMqConfiguration,
        ILogger logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _rabbitMqConfiguration = rabbitMqConfiguration ?? throw new ArgumentNullException(nameof(rabbitMqConfiguration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (LogContext.PushProperty(RabbitMqSyncToAsyncSink.InternalLogProperty, value: true))
        {
            _logger.Information("{ServiceName}.{MethodName}: BackgroundService started",
                nameof(RabbitMqBufferSynchronizer), nameof(ExecuteAsync));
        }

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
                    catch (Exception ex)
                    {
                        // Don't stop worker in case of error
                        using (LogContext.PushProperty(RabbitMqSyncToAsyncSink.InternalLogProperty, value: true))
                        {
                            _logger.Error(ex, "{ServiceName}.{MethodName}: Failed to publish message. Message will be dropped",
                                nameof(RabbitMqBufferSynchronizer), nameof(ExecuteAsync));
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (Exception ex)
            {
                using (LogContext.PushProperty(RabbitMqSyncToAsyncSink.InternalLogProperty, value: true))
                {
                    _logger.Error(ex, "{ServiceName}.{MethodName}: Exception thrown",
                        nameof(RabbitMqBufferSynchronizer), nameof(ExecuteAsync));
                }
            }
        }

        using (LogContext.PushProperty(RabbitMqSyncToAsyncSink.InternalLogProperty, value: true))
        {
            _logger.Information("{ServiceName}.{MethodName}: BackgroundService ended",
                nameof(RabbitMqBufferSynchronizer), nameof(ExecuteAsync));
        }
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
        RabbitMqConfiguration rabbitMqConfiguration,
        ILogger logger)
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

            return new RabbitMqBufferSynchronizer(connection, channel, queue, rabbitMqConfiguration, logger);
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