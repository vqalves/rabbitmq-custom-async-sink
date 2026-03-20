using System.Net.Http.Headers;
using ApiWithLog.Logging;
using ApiWithLog.Middlewares;
using ApiWithLog.Middlewares.RegisterResponseRequest;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var rabbitMqConnectionString = "amqp://guest:guest@localhost:5672";

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console(
        restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.RabbitMQWithBackgroundService(
        rabbitMqConnectionString: rabbitMqConnectionString,
        rabbitMqQueueName: "application-logs",
        bufferMaximumSize: 700,
        logFormatterForRabbitMQ: new LogFormatterForRabbitMQDefault(
            formatProvider: null
        ),
        minimumLevel: LogEventLevel.Debug,
        hostedServices: builder.Services);

Log.Logger = loggerConfig.CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.RegisterResponseRequestMiddlewareDependencies(
    bufferMaxSize: 100,
    rabbitMqConnectionString: rabbitMqConnectionString,
    rabbitMqQueueName: "request-response-logs"
);

// Register exception handler
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<RegisterResponseRequestMiddleware>(); // RegisterResponseRequestMiddleware must be declared before everyone else (outer layer)
app.UseExceptionHandler();

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

app.MapGet("/stress-log", (ILogger<Program> logger) =>
{
    for(var i = 0; i < 1_000; i++)
        logger.LogDebug("Stress test #{testNumber}", i);

    return Results.Ok();
})
.WithName("StressLog")
.WithOpenApi();

app.MapPost("/post", ([FromBody] PostData postData, ILogger<Program> logger) =>
{
    return Results.Content($"Your post was: '{postData.content}'", "text/plain");
})
.WithName("Post")
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
