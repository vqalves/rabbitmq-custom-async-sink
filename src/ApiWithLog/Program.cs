using ApiWithLog.Logging;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information);

await loggerConfig.WriteTo.RabbitMQWithBackgroundServiceAsync(
    hostName: "localhost",
    port: 5672,
    userName: "guest",
    password: "guest",
    queueName: "application-logs",
    bufferMaximumSize: 700,
    logFormatterForRabbitMQDefault: new LogFormatterForRabbitMQDefault(
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

app.MapGet("/stress-log", (ILogger<Program> logger) =>
{
    for(var i = 0; i < 1_000; i++)
        logger.LogDebug("Stress test #{testNumber}", i);

    return Results.Ok();
})
.WithName("StressLog")
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
