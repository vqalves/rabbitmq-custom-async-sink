using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace ApiWithLog.Logging;

/// <summary>
/// Custom Serilog sink that retrieves logs from buffers and sends them to RabbitMQ asynchronously via a background synchronizer.
/// </summary>
public class RabbitMqBufferQueueSink : ILogEventSink
{
    private readonly RabbitMqBufferQueue _buffer;
    private readonly LogEventLevel _minimumLevel;
    private readonly ILogFormatterForRabbitMQ _logFormatterForRabbitMQ;
    private readonly ILogger _logger;

    public RabbitMqBufferQueueSink(
        RabbitMqBufferQueue buffer,
        ILogFormatterForRabbitMQ logFormatterForRabbitMQ,
        LogEventLevel minimumLevel,
        ILogger logger)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _logFormatterForRabbitMQ = logFormatterForRabbitMQ ?? throw new ArgumentNullException(nameof(logFormatterForRabbitMQ));
        _minimumLevel = minimumLevel;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Emit(LogEvent logEvent)
    {
        // Check if log event meets minimum level requirement
        if (logEvent.Level < _minimumLevel)
            return;

        try
        {
            var messageData = _logFormatterForRabbitMQ.Format(logEvent);
            _buffer.Enqueue(messageData);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "{SinkName}.{MethodName}: Error handling log event", nameof(RabbitMqBufferQueueSink), nameof(Emit));
        }
    }
}

public static class RabbitMqSyncToAsyncSinkExtensions
{
    public static LoggerConfiguration RabbitMQWithBackgroundService(
        this LoggerSinkConfiguration sinkConfiguration,
        string hostName,
        int port,
        string userName,
        string password,
        string queueName,
        int bufferMaximumSize,
        ILogFormatterForRabbitMQ logFormatterForRabbitMQ,
        LogEventLevel minimumLevel,
        IServiceCollection hostedServices)
    {
        var rabbitMqConfig = RabbitMqConfiguration.Create(
            hostName: hostName,
            port: port,
            userName: userName,
            password: password,
            queueName: queueName
        );

        return RabbitMQWithBackgroundService(
            sinkConfiguration: sinkConfiguration,
            rabbitMqConfig: rabbitMqConfig,
            bufferMaximumSize: bufferMaximumSize,
            logFormatterForRabbitMQ: logFormatterForRabbitMQ,
            minimumLevel: minimumLevel,
            hostedServices: hostedServices
        );
    }

    public static LoggerConfiguration RabbitMQWithBackgroundService(
        this LoggerSinkConfiguration sinkConfiguration,
        string rabbitMqConnectionString,
        string rabbitMqQueueName,
        int bufferMaximumSize,
        ILogFormatterForRabbitMQ logFormatterForRabbitMQ,
        LogEventLevel minimumLevel,
        IServiceCollection hostedServices)
    {
        var rabbitMqConfig = RabbitMqConfiguration.Create(
            connectionString: rabbitMqConnectionString,
            queueName: rabbitMqQueueName);

        return RabbitMQWithBackgroundService(
            sinkConfiguration: sinkConfiguration,
            rabbitMqConfig: rabbitMqConfig,
            bufferMaximumSize: bufferMaximumSize,
            logFormatterForRabbitMQ: logFormatterForRabbitMQ,
            minimumLevel: minimumLevel,
            hostedServices: hostedServices
        );
    }

    public static LoggerConfiguration RabbitMQWithBackgroundService(
        this LoggerSinkConfiguration sinkConfiguration,
        RabbitMqConfiguration rabbitMqConfig,
        int bufferMaximumSize,
        ILogFormatterForRabbitMQ logFormatterForRabbitMQ,
        LogEventLevel minimumLevel,
        IServiceCollection hostedServices)
    {
        var bootstrapLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        var bufferQueue = new RabbitMqBufferQueue(
            bufferQueueName: "Logging Buffer",
            bufferMaxSize: bufferMaximumSize,
            logger: bootstrapLogger);

        var sink = new RabbitMqBufferQueueSink(
            buffer: bufferQueue,
            logFormatterForRabbitMQ: logFormatterForRabbitMQ,
            minimumLevel: minimumLevel,
            logger: bootstrapLogger
        );

        var syncWorker = new RabbitMqPublishBackgroundServiceWrapper<RabbitMqBufferQueueSink>(
            serviceName: "Logging BackgroundService",
            queue: bufferQueue,
            rabbitMqConfiguration: rabbitMqConfig,
            logger: bootstrapLogger);

        var result = sinkConfiguration.Sink(sink, minimumLevel);
        hostedServices.AddHostedService(p => syncWorker);

        return result;
    }
}