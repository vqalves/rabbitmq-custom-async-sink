using Serilog.Events;

namespace ApiWithLog.Logging;

/// <summary>
/// Configuration for RabbitMQ connection and logging
/// </summary>
public interface ILogFormatterForRabbitMQ
{
    byte[] Format(LogEvent logEvent);
}