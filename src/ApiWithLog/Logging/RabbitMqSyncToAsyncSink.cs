using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace ApiWithLog.Logging;

/// <summary>
/// Custom Serilog sink that retrieves logs from buffers and sends them to RabbitMQ asynchronously via a background synchronizer.
/// </summary>
public class RabbitMqSyncToAsyncSink : ILogEventSink
{
    private readonly SyncToAsyncBufferQueue _buffer;
    private readonly LogEventLevel _minimumLevel;
    private readonly ILogFormatterForRabbitMQ _logFormatterForRabbitMQ;

    public RabbitMqSyncToAsyncSink(
        SyncToAsyncBufferQueue buffer,
        ILogFormatterForRabbitMQ logFormatterForRabbitMQ,
        LogEventLevel minimumLevel)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _logFormatterForRabbitMQ = logFormatterForRabbitMQ ?? throw new ArgumentNullException(nameof(logFormatterForRabbitMQ));
        _minimumLevel = minimumLevel;
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
            Console.Error.WriteLine($"{nameof(RabbitMqSyncToAsyncSink)}.{nameof(Emit)}: Error handling log event. Error: {ex.Message}");
        }
    }
}

public static class RabbitMqSyncToAsyncSinkExtensions
{
    public static async Task RabbitMQWithBackgroundServiceAsync(
        this LoggerSinkConfiguration sinkConfiguration,
        string hostName,
        int port,
        string userName,
        string password,
        string queueName,
        int bufferMaximumSize,
        LogFormatterForRabbitMQDefault logFormatterForRabbitMQDefault,
        LogEventLevel minimumLevel,
        IServiceCollection hostedServices)
    {
        var rabbitMqConfig = new RabbitMqConfiguration(
            hostName: hostName,
            port: port,
            userName: userName, 
            password: password, 
            queueName: queueName
        );

        await RabbitMQWithBackgroundServiceAsync(
            sinkConfiguration: sinkConfiguration,
            rabbitMqConfig: rabbitMqConfig,
            bufferMaximumSize: bufferMaximumSize,
            logFormatterForRabbitMQDefault: logFormatterForRabbitMQDefault,
            minimumLevel: minimumLevel,
            hostedServices: hostedServices
        );
    }

    public static async Task RabbitMQWithBackgroundServiceAsync(
        this LoggerSinkConfiguration sinkConfiguration,
        string rabbitMqConnectionString,
        string rabbitMqQueueName,
        int bufferMaximumSize,
        LogFormatterForRabbitMQDefault logFormatterForRabbitMQDefault,
        LogEventLevel minimumLevel,
        IServiceCollection hostedServices)
    {
        var rabbitMqConfig = new RabbitMqConfiguration(rabbitMqConnectionString, rabbitMqQueueName);

        await RabbitMQWithBackgroundServiceAsync(
            sinkConfiguration: sinkConfiguration,
            rabbitMqConfig: rabbitMqConfig,
            bufferMaximumSize: bufferMaximumSize,
            logFormatterForRabbitMQDefault: logFormatterForRabbitMQDefault,
            minimumLevel: minimumLevel,
            hostedServices: hostedServices
        );
    }

    public static async Task RabbitMQWithBackgroundServiceAsync(
        this LoggerSinkConfiguration sinkConfiguration,
        RabbitMqConfiguration rabbitMqConfig,
        int bufferMaximumSize,
        LogFormatterForRabbitMQDefault logFormatterForRabbitMQDefault,
        LogEventLevel minimumLevel,
        IServiceCollection hostedServices)
    {
        var bufferQueue = new SyncToAsyncBufferQueue(bufferMaximumSize);

        var sink = new RabbitMqSyncToAsyncSink(
            buffer: bufferQueue,
            logFormatterForRabbitMQ: logFormatterForRabbitMQDefault,
            minimumLevel: minimumLevel
        );

        var syncWorker = await RabbitMqBufferSynchronizer.CreateNewAsync(bufferQueue, rabbitMqConfig);

        /*
        sinkConfiguration.Logger(c => c.Filter
            .ByExcluding(Matching.WithProperty("FileOnly"))
            .WriteTo.Sink(sink, minimumLevel)
        );
        */

        sinkConfiguration.Sink(sink, minimumLevel);
        hostedServices.AddHostedService(p => syncWorker);
    }
}