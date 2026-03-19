namespace ApiWithLog.Logging;

/// <summary>
/// Configuration for RabbitMQ connection and logging
/// </summary>
public class RabbitMqConfiguration
{
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// RabbitMQ host name (default: localhost)
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ port (default: 5672)
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// RabbitMQ user name (default: guest)
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// RabbitMQ password (default: guest)
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Queue name where logs will be sent (default: application-logs)
    /// </summary>
    public string QueueName { get; set; } = "application-logs";

    /// <summary>
    /// Minimum log level to send to RabbitMQ (default: Information)
    /// Options: Debug, Information, Warning, Error, Fatal
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Enable or disable RabbitMQ logging (default: true)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Use custom sink (true) or standard Serilog.Sinks.RabbitMQ (false)
    /// Custom sink uses array format: ["applicationName", "clientDateTime", "logMessage"]
    /// Standard sink uses JSON object format
    /// </summary>
    public bool UseCustomSink { get; set; } = true;
}
