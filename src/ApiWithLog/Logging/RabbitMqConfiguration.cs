using System;

namespace ApiWithLog.Logging;

/// <summary>
/// Configuration for RabbitMQ connection and logging
/// </summary>
public class RabbitMqConfiguration
{
    /// <summary>
    /// RabbitMQ host name (default: localhost)
    /// </summary>
    public string HostName { get; set; }

    /// <summary>
    /// RabbitMQ port (default: 5672)
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// RabbitMQ user name (default: guest)
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// RabbitMQ password (default: guest)
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Queue name where logs will be sent (default: application-logs)
    /// </summary>
    public string QueueName { get; set; }

    public RabbitMqConfiguration(string hostName, int port, string userName, string password, string queueName)
    {
        HostName = hostName;
        Port = port;
        UserName = userName;
        Password = password;
        QueueName = queueName;
    }

    public RabbitMqConfiguration(string connectionString, string queueName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Queue name cannot be null or empty", nameof(queueName));
        }

        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) || uri.Scheme != "amqp")
        {
            throw new ArgumentException("Connection string must be a valid AMQP URI (e.g., amqp://user:password@host:port)", nameof(connectionString));
        }

        // Validate and parse UserInfo (username:password)
        if (string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("Connection string must include credentials (username:password)", nameof(connectionString));
        }

        if (!uri.UserInfo.Contains(':'))
        {
            throw new ArgumentException("Connection string credentials must be in 'username:password' format", nameof(connectionString));
        }

        var userInfoParts = uri.UserInfo.Split(':', 2);
        if (string.IsNullOrWhiteSpace(userInfoParts[0]) || string.IsNullOrWhiteSpace(userInfoParts[1]))
        {
            throw new ArgumentException("Both username and password must be provided in connection string", nameof(connectionString));
        }

        HostName = uri.Host;
        Port = uri.Port > 0 ? uri.Port : 5672;
        UserName = userInfoParts[0];
        Password = userInfoParts[1];
        QueueName = queueName;
    }
}