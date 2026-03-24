using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using ILogger = Serilog.ILogger;

namespace ApiWithLog.Logging;

public class RabbitMqPublishBackgroundService : BackgroundService
{
    private readonly RabbitMqBufferQueue _queue;
    private readonly RabbitMqConfiguration _rabbitMqConfiguration;
    private readonly string _serviceName;
    private readonly ILogger _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    public RabbitMqPublishBackgroundService(
        string serviceName,
        RabbitMqBufferQueue queue,
        RabbitMqConfiguration rabbitMqConfiguration,
        ILogger logger)
    {
        _serviceName = serviceName;
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _rabbitMqConfiguration = rabbitMqConfiguration ?? throw new ArgumentNullException(nameof(rabbitMqConfiguration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqConfiguration.HostName,
            Port = _rabbitMqConfiguration.Port,
            UserName = _rabbitMqConfiguration.UserName,
            Password = _rabbitMqConfiguration.Password
        };

        try
        {
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "{ServiceName}: Failed to initialize RabbitMQ connection", _serviceName);

            // Clean up partial resources
            try { _channel?.Dispose(); }
            catch { /* Ignore disposal errors */ }

            try { _connection?.Dispose(); }
            catch { /* Ignore disposal errors */ }

            _channel = null;
            _connection = null;

            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        try { _channel?.Dispose(); }
        catch { }

        try { _connection?.Dispose(); }
        catch { }

        _channel = null;
        _connection = null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("{ServiceName}: BackgroundService started", _serviceName);

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
                        if (_channel == null)
                        {
                            _logger.Warning("{ServiceName}: Channel is null, message will be dropped", _serviceName);
                            continue;
                        }

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
                        _logger.Error(ex, "{ServiceName}: Failed to publish message. Message will be dropped", _serviceName);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "{ServiceName}: Exception thrown", _serviceName);
            }
        }

        _logger.Information("{ServiceName}: BackgroundService ended", _serviceName);
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

}