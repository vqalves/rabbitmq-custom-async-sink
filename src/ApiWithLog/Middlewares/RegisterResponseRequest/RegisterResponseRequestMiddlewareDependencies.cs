using System.Text;
using System.Text.Json;
using ApiWithLog.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.ILogger;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ApiWithLog.Middlewares.RegisterResponseRequest;

public class RegisterResponseRequestMiddlewareDependencies
{
    private readonly Logger SerilogLogger;
    private readonly RabbitMqBufferQueue Queue;
    private readonly JsonSerializerOptions Options;

    public RegisterResponseRequestMiddlewareDependencies(int bufferMaxSize, ILogger logger)
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
    }

    public void Register(RegisterResponseRequestMiddlewareData data)
    {
        var json = JsonSerializer.Serialize(data, Options);
        var bytes = Encoding.UTF8.GetBytes(json);
        Queue.Enqueue(bytes);
    }

    public async Task<RabbitMqPublishBackgroundService> CreatePublisherBackgroundServiceAsync(
        RabbitMqConfiguration rabbitMqConfiguration)
    {
        var result = await RabbitMqPublishBackgroundService.CreateNewAsync(
            serviceName: "RequestResponse BackgroundService",
            queue: Queue, 
            rabbitMqConfiguration: rabbitMqConfiguration, 
            logger: SerilogLogger);

        return result;
    }
}

public static class RegisterResponseRequestMiddlewareDependenciesExtensions
{
    public static async Task RegisterResponseRequestMiddlewareDependenciesAsync(
        this IServiceCollection serviceCollection,
        int bufferMaxSize,
        string rabbitMqHostName, 
        int rabbitMqPort,
        string rabbitMqUserName,
        string rabbitMqPassword,
        string rabbitMqQueueName
    )
    {
        var rabbitConfig = RabbitMqConfiguration.Create(rabbitMqHostName, rabbitMqPort, rabbitMqUserName, rabbitMqPassword, rabbitMqQueueName);
        await RegisterResponseRequestMiddlewareDependenciesAsync(serviceCollection, bufferMaxSize, rabbitConfig);
    }

    public static async Task RegisterResponseRequestMiddlewareDependenciesAsync(
        this IServiceCollection serviceCollection,
        int bufferMaxSize,
        string rabbitMqConnectionString,
        string rabbitMqQueueName
    )
    {
        var rabbitConfig = RabbitMqConfiguration.Create(rabbitMqConnectionString, rabbitMqQueueName);
        await RegisterResponseRequestMiddlewareDependenciesAsync(serviceCollection, bufferMaxSize, rabbitConfig);
    }

    public static async Task RegisterResponseRequestMiddlewareDependenciesAsync(
        this IServiceCollection serviceCollection,
        int bufferMaxSize,
        RabbitMqConfiguration rabbitMqConfig
    )
    {
        var sp = serviceCollection.BuildServiceProvider();

        var logger = sp.GetRequiredService<ILogger<RegisterResponseRequestMiddlewareDependencies>>();
        var dependencies = new RegisterResponseRequestMiddlewareDependencies(bufferMaxSize, logger);
        var publisherService = await dependencies.CreatePublisherBackgroundServiceAsync(rabbitMqConfig);

        serviceCollection.AddSingleton(dependencies);
        serviceCollection.AddHostedService(p => publisherService);
    }
}