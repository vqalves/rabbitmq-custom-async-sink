using Serilog.Events;

namespace ApiWithLog.Logging;

/// <summary>
/// Configuration for RabbitMQ connection and logging
/// </summary>
public interface IRabbitMqMessageFormatter<T>
{
    byte[] Format(T obj);
}