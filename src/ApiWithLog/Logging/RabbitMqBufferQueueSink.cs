using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Filters;
using ILogger = Serilog.ILogger;

namespace ApiWithLog.Logging;

/// <summary>
/// Custom Serilog sink that retrieves logs from buffers and sends them to RabbitMQ asynchronously via a background synchronizer.
/// </summary>
public class RabbitMqBufferQueueSink : ILogEventSink
{
    private readonly RabbitMqBufferQueue _buffer;
    private readonly LogEventLevel _minimumLevel;
    private readonly IRabbitMqMessageFormatter<LogEvent> _logFormatter;
    private readonly ILogger _logger;

    public RabbitMqBufferQueueSink(
        RabbitMqBufferQueue buffer,
        IRabbitMqMessageFormatter<LogEvent> logFormatter,
        LogEventLevel minimumLevel,
        ILogger logger)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _logFormatter = logFormatter ?? throw new ArgumentNullException(nameof(logFormatter));
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
            var messageData = _logFormatter.Format(logEvent);
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
        IRabbitMqMessageFormatter<LogEvent> logFormatter,
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
            logFormatter: logFormatter,
            minimumLevel: minimumLevel,
            hostedServices: hostedServices
        );
    }

    public static LoggerConfiguration RabbitMQWithBackgroundService(
        this LoggerSinkConfiguration sinkConfiguration,
        string rabbitMqConnectionString,
        string rabbitMqQueueName,
        int bufferMaximumSize,
        IRabbitMqMessageFormatter<LogEvent> logFormatter,
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
            logFormatter: logFormatter,
            minimumLevel: minimumLevel,
            hostedServices: hostedServices
        );
    }

    public static LoggerConfiguration RabbitMQWithBackgroundService(
        this LoggerSinkConfiguration sinkConfiguration,
        RabbitMqConfiguration rabbitMqConfig,
        int bufferMaximumSize,
        IRabbitMqMessageFormatter<LogEvent> logFormatter,
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
            logFormatter: logFormatter,
            minimumLevel: minimumLevel,
            logger: bootstrapLogger
        );

        var syncWorker = new RabbitMqPublishBackgroundServiceWrapper<RabbitMqBufferQueueSink>(
            serviceName: "Logging BackgroundService",
            queue: bufferQueue,
            rabbitMqConfiguration: rabbitMqConfig,
            logger: bootstrapLogger);

        var result = sinkConfiguration.Logger(p =>
        {
            p
             .Filter.ByExcluding(Matching.FromSource("System"))
             .Filter.ByExcluding(Matching.FromSource("Microsoft"))
             .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore"))
             .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Hosting.Diagnostics"))
             .Filter.ByExcluding(Matching.FromSource("Microsoft.Extensions.Hosting.Internal.Host"))
             .WriteTo.Sink(sink, minimumLevel);
        });

        hostedServices.AddHostedService(p => syncWorker);

        return result;
    }
}