using ApiWithLog.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Get RabbitMQ configuration
var rabbitMqConfig = builder.Configuration
    .GetSection(RabbitMqConfiguration.SectionName)
    .Get<RabbitMqConfiguration>() ?? new RabbitMqConfiguration();

// Get application name from environment variable
var applicationName = Environment.GetEnvironmentVariable("LOG_APPLICATION_NAME")
                      ?? builder.Environment.ApplicationName;

// Configure Serilog
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console();

using var sink = await CustomRabbitMqSink.CreateAsync(
    hostName: rabbitMqConfig.HostName,
    port: rabbitMqConfig.Port,
    userName: rabbitMqConfig.UserName,
    password: rabbitMqConfig.Password,
    queueName: rabbitMqConfig.QueueName,
    applicationName: applicationName,
    formatProvider: null);

// Use custom sink with array format: ["applicationName", "clientDateTime", "logMessage"]
loggerConfig.WriteTo.CustomRabbitMq(sink);

// Add RabbitMQ sink if enabled
/*
if (rabbitMqConfig.Enabled)
{
    var minimumLevel = Enum.Parse<LogEventLevel>(rabbitMqConfig.MinimumLevel, true);

    if (rabbitMqConfig.UseCustomSink)
    {
        
    }
    else
    {
        // Use standard Serilog.Sinks.RabbitMQ (JSON object format)
        var rabbitMqSinkConfig = new RabbitMQClientConfiguration
        {
            DeliveryMode = RabbitMQDeliveryMode.Durable,
            Exchange = rabbitMqConfig.QueueName,
            ExchangeType = "direct"
        };

        loggerConfig.WriteTo.RabbitMQ((clientConfiguration, sinkConfiguration) =>
        {
            clientConfiguration.Hostnames.Add(rabbitMqConfig.HostName);
            clientConfiguration.Port = rabbitMqConfig.Port;
            clientConfiguration.Username = rabbitMqConfig.UserName;
            clientConfiguration.Password = rabbitMqConfig.Password;
            clientConfiguration.DeliveryMode = RabbitMQDeliveryMode.Durable;
            clientConfiguration.Exchange = rabbitMqConfig.QueueName;
            clientConfiguration.ExchangeType = "direct";

            sinkConfiguration.RestrictedToMinimumLevel = minimumLevel;
        });
    }
}
*/

Log.Logger = loggerConfig.CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (ILogger<Program> logger) =>
{
    logger.LogInformation("WeatherForecast endpoint called");

    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    logger.LogDebug("Generated {ForecastCount} weather forecasts", forecast.Length);
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapGet("/error", (ILogger<Program> logger) =>
{
    try
    {
        var val1 = 0;
        var val2 = 0;
        var val3 = val1 / val2;
        return val3;
    }
    catch(Exception ex)
    {
        logger.LogError(ex, "Error happened");
        throw;
    }
})
.WithName("Error")
.WithOpenApi();

try
{
    Log.Information("Starting web application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
