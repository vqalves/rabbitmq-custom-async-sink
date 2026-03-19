namespace ApiWithLog.Logging;

/// <summary>
/// Configuration for RabbitMQ connection and logging
/// </summary>
public class RabbitMqConfiguration
{
    public string HostName { get; init; }
    public int Port { get; init; }
    public string UserName { get; init; }
    public string Password { get; init; }
    public string QueueName { get; init; }

    private RabbitMqConfiguration(string hostName, int port, string userName, string password, string queueName)
    {
        HostName = hostName;
        Port = port;
        UserName = userName;
        Password = password;
        QueueName = queueName;
    }

    public static RabbitMqConfiguration Create(string hostName, int port, string userName, string password, string queueName)
    {
        if (string.IsNullOrWhiteSpace(hostName))
            throw new ArgumentException("Host name cannot be null or empty", nameof(hostName));

        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535");

        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("User name cannot be null or empty", nameof(userName));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        if (string.IsNullOrWhiteSpace(queueName))
            throw new ArgumentException("Queue name cannot be null or empty", nameof(queueName));

        return new RabbitMqConfiguration(hostName, port, userName, password, queueName);
    }

    public static RabbitMqConfiguration Create(string connectionString, string queueName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) || uri.Scheme != "amqp")
            throw new ArgumentException("Connection string must be a valid AMQP URI (e.g., amqp://user:password@host:port)", nameof(connectionString));

        if (string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Connection string must include credentials (username:password)", nameof(connectionString));

        if (!uri.UserInfo.Contains(':'))
            throw new ArgumentException("Connection string credentials must be in 'username:password' format", nameof(connectionString));

        var userInfoParts = uri.UserInfo.Split(':', 2);
        if (string.IsNullOrWhiteSpace(userInfoParts[0]) || string.IsNullOrWhiteSpace(userInfoParts[1]))
            throw new ArgumentException("Both username and password must be provided in connection string", nameof(connectionString));

        var hostName = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5672;
        var userName = userInfoParts[0];
        var password = userInfoParts[1];

        return Create(hostName, port, userName, password, queueName);
    }
}