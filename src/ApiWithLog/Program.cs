using ApiWithLog.Logging;
using ApiWithLog.Middlewares;
using ApiWithLogger.Middlewares.RegisterResponseRequest;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var rabbitMqConnectionString = "amqp://guest:guest@localhost:5672";

// #####################################
//
//  Configure Serilog
//
// #####################################

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console(
        restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.RabbitMQWithBackgroundService(
        rabbitMqConnectionString: rabbitMqConnectionString,
        rabbitMqQueueName: "application-logs",
        bufferMaximumSize: 700,
        logFormatter: new RabbitMqMessageFormatterDefault(formatProvider: null),
        minimumLevel: LogEventLevel.Error,
        hostedServices: builder.Services);

Log.Logger = loggerConfig.CreateLogger();

builder.Host.UseSerilog();

// Opcional: Se estiver usando ILogger (sem generics) na inje��o de depend�ncia, pode ser necess�rio registrar
// builder.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(p => p.GetRequiredService<ILogger<object>>());





// #####################################
//
//  Configura��o Swagger
//
// #####################################

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();





// #####################################
//
//  Configura��o Response/Request Middleware (h� mais sess�es abaixo)
//
// #####################################

builder.Services.RegisterResponseRequestMiddlewareDependencies(
    bufferMaxSize: 100,
    rabbitMqConnectionString: rabbitMqConnectionString,
    rabbitMqQueueName: "request-response-logs",
    messageFormatter: new RegisterResponseRequestMessageFormatter()
);



// #####################################
//
//  Configura��o Exception Middleware (h� mais sess�es abaixo)
//
// #####################################

builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddProblemDetails();





var app = builder.Build();

// #####################################
//
//  Configure Swagger
//
// #####################################
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// #####################################
//
//  Configura��o Response/Request Middleware
//
// #####################################

// Deve ser declarado antes dos outros Middlewares
app.UseMiddleware<RegisterResponseRequestMiddleware>();


// #####################################
//
//  Configura��o Exception Middleware
//
// #####################################
app.UseExceptionHandler();





app.UseHttpsRedirection();

app.MapGet("/", () =>
{
    return "Hello World!";
});

app.MapGet("/log-info", (ILogger<Program> log) =>
{
    log.LogInformation("[Information] Standalone log in {CurrentDate}", DateTime.Now);

    return Results.Ok();
});

app.MapGet("/exception", () =>
{
    throw new NotImplementedException();
});

app.MapGet("/example-request", () =>
{
    return "Exemplo de request";
});

app.MapGet("/dump", (ILogger<Program> log) =>
{
    log.LogTrace("[Trace] Logged in {CurrentDate}", DateTime.Now);
    log.LogDebug("[Debug] Logged in {CurrentDate}", DateTime.Now);
    log.LogInformation("[Information] Logged in {CurrentDate}", DateTime.Now);
    log.LogWarning("[Warning] Logged in {CurrentDate}", DateTime.Now);
    log.LogError("[Error] Logged in {CurrentDate}", DateTime.Now);
    log.LogCritical("[Critical] Logged in {CurrentDate}", DateTime.Now);

    throw new NotImplementedException();
});

app.Run();
