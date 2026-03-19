using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace ApiWithLog.Logging;

/// <summary>
/// Custom Serilog sink that sends logs to RabbitMQ in a specific array format:
/// ["applicationName", "clientDateTime", "logMessage"]
/// </summary>
public class CustomRabbitMqSink : ILogEventSink, IDisposable
{
    private IConnection _connection;
    private IChannel _channel;
    private string _queueName;
    private string _applicationName;
    private IFormatProvider? _formatProvider;
    private LogEventLevel _minimumLevel;
    private bool _disposed;

    public static async Task<CustomRabbitMqSink> CreateAsync(
        string hostName,
        int port,
        string userName,
        string password,
        string queueName,
        string applicationName,
        LogEventLevel minimumLevel,
        IFormatProvider? formatProvider = null)
    {
        var sink = new CustomRabbitMqSink();
        sink._queueName = queueName;
        sink._applicationName = applicationName;
        sink._minimumLevel = minimumLevel;
        sink._formatProvider = formatProvider;

        var factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = port,
            UserName = userName,
            Password = password
        };

        sink._connection = await factory.CreateConnectionAsync();
        sink._channel = await sink._connection.CreateChannelAsync();

        return sink;
    }

    public void Emit(LogEvent logEvent)
    {
        if (_disposed)
            return;

        // Check if log event meets minimum level requirement
        if (logEvent.Level < _minimumLevel)
            return;

        try
        {
            // Format the log message with all properties
            var logMessage = logEvent.RenderMessage(_formatProvider);

            // Create the array in the format: ["applicationName", "clientDateTime", "logMessage"]
            var messageArray = new object[]
            {
                _applicationName,
                DateTime.Now.ToString("O"), // ISO 8601 format
                logMessage
            };

            // Serialize to JSON
            var jsonMessage = JsonSerializer.Serialize(messageArray);
            var body = Encoding.UTF8.GetBytes(jsonMessage);

            // Publish to queue
            _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _queueName,
                body: body).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // In a production environment, you might want to log this to a fallback sink
            Console.Error.WriteLine($"Error publishing to RabbitMQ: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error disposing RabbitMQ connection: {ex.Message}");
        }
    }
}

public static class CustomRabbitMqSinkExtensions
{
    /// <summary>
    /// Adds a custom RabbitMQ sink that sends logs in the format: ["applicationName", "clientDateTime", "logMessage"]
    /// </summary>
    public static LoggerConfiguration CustomRabbitMq(
        this LoggerSinkConfiguration sinkConfiguration,
        CustomRabbitMqSink sink,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
    {
        return sinkConfiguration.Sink(sink, restrictedToMinimumLevel);
    }
}
