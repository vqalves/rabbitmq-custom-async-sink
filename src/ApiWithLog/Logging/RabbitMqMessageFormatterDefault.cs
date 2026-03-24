using System.Text;
using System.Text.Json;
using Serilog.Events;

namespace ApiWithLog.Logging;

/// <summary>
/// Configuration for RabbitMQ connection and logging
/// </summary>
public class RabbitMqMessageFormatterDefault : IRabbitMqMessageFormatter<LogEvent>
{
    private readonly IFormatProvider? _formatProvider;

    public RabbitMqMessageFormatterDefault(IFormatProvider? formatProvider)
    {
        _formatProvider = formatProvider;
    }

    public byte[] Format(LogEvent logEvent)
    {
        // Format the log message with all properties
        var logMessage = logEvent.RenderMessage(_formatProvider);

        // Serialize to JSON
        var logData = new
        {
            Content = logMessage
        };

        var jsonMessage = JsonSerializer.Serialize(logData);
        return Encoding.UTF8.GetBytes(jsonMessage);
    }
}