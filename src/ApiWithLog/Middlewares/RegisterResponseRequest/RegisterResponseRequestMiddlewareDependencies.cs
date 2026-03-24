using System.Text;
using System.Text.Json;
using ApiWithLog.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.ILogger;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ApiWithLogger.Middlewares.RegisterResponseRequest;

public class RegisterResponseRequestMiddlewareDependencies
{
    private readonly Logger SerilogLogger;
    private readonly RabbitMqBufferQueue Queue;
    private readonly JsonSerializerOptions Options;
    private readonly IRabbitMqMessageFormatter<RegisterResponseRequestMiddlewareData> MessageFormatter;

    public RegisterResponseRequestMiddlewareDependencies(int bufferMaxSize, ILogger logger, IRabbitMqMessageFormatter<RegisterResponseRequestMiddlewareData> messageFormatter)
    {
        SerilogLogger = new LoggerConfiguration()
            .WriteTo.ILogger(logger)
            .CreateLogger();

        Queue = new RabbitMqBufferQueue(
            bufferQueueName: "RequestResponse Buffer",
            bufferMaxSize: bufferMaxSize, 
            logger: SerilogLogger);

        Options = new JsonSerializerOptions()
        {
            WriteIndented = false
        };

        MessageFormatter = messageFormatter;
    }

    public void Register(RegisterResponseRequestMiddlewareData data)
    {
        var bytes = MessageFormatter.Format(data);
        Queue.Enqueue(bytes);
    }

    public RabbitMqPublishBackgroundServiceWrapper<RegisterResponseRequestMiddlewareDependencies> CreatePublisherBackgroundService(
        RabbitMqConfiguration rabbitMqConfiguration)
    {
        var result = new RabbitMqPublishBackgroundServiceWrapper<RegisterResponseRequestMiddlewareDependencies>(
            serviceName: "RequestResponse BackgroundService",
            queue: Queue,
            rabbitMqConfiguration: rabbitMqConfiguration,
            logger: SerilogLogger);

        return result;
    }
}

public static class RegisterResponseRequestMiddlewareDependenciesExtensions
{
    public static void RegisterResponseRequestMiddlewareDependencies(
        this IServiceCollection serviceCollection,
        int bufferMaxSize,
        string rabbitMqHostName,
        int rabbitMqPort,
        string rabbitMqUserName,
        string rabbitMqPassword,
        string rabbitMqQueueName,
        IRabbitMqMessageFormatter<RegisterResponseRequestMiddlewareData> messageFormatter
    )
    {
        var rabbitConfig = RabbitMqConfiguration.Create(rabbitMqHostName, rabbitMqPort, rabbitMqUserName, rabbitMqPassword, rabbitMqQueueName);
        RegisterResponseRequestMiddlewareDependencies(serviceCollection, bufferMaxSize, rabbitConfig, messageFormatter);
    }

    public static void RegisterResponseRequestMiddlewareDependencies(
        this IServiceCollection serviceCollection,
        int bufferMaxSize,
        string rabbitMqConnectionString,
        string rabbitMqQueueName,
        IRabbitMqMessageFormatter<RegisterResponseRequestMiddlewareData> messageFormatter
    )
    {
        var rabbitConfig = RabbitMqConfiguration.Create(rabbitMqConnectionString, rabbitMqQueueName);
        RegisterResponseRequestMiddlewareDependencies(serviceCollection, bufferMaxSize, rabbitConfig, messageFormatter);
    }

    public static void RegisterResponseRequestMiddlewareDependencies(
        this IServiceCollection serviceCollection,
        int bufferMaxSize,
        RabbitMqConfiguration rabbitMqConfig,
        IRabbitMqMessageFormatter<RegisterResponseRequestMiddlewareData> messageFormatter
    )
    {
        serviceCollection.AddSingleton(p =>
        {
            var logger = p.GetRequiredService<ILogger<RegisterResponseRequestMiddlewareDependencies>>();
            return new RegisterResponseRequestMiddlewareDependencies(bufferMaxSize, logger, messageFormatter);
        });

        serviceCollection.AddHostedService(p =>
        {
            var dependencies = p.GetRequiredService<RegisterResponseRequestMiddlewareDependencies>();
            return dependencies.CreatePublisherBackgroundService(rabbitMqConfig);
        });
    }
}