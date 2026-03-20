using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApiWithLog.Middlewares;

public class UnhandledExceptionHandler : IExceptionHandler
{
    private readonly ILogger<UnhandledExceptionHandler> _logger;

    public UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Generate a correlation GUID for tracking
        var correlationId = Guid.NewGuid().ToString();

        // Capture the request URL for context
        var url = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}{httpContext.Request.QueryString}";
        var method = httpContext.Request.Method;

        // Log the error with correlation ID and request details
        _logger.LogError(
            exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Method: {Method}, URL: {Url}",
            correlationId,
            method,
            url);

        // Create ProblemDetails with correlation ID in extensions
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Detail = "An internal error occurred while processing your request.",
            Instance = httpContext.Request.Path,
            Extensions = new Dictionary<string, object?>
            {
                ["correlationId"] = correlationId,
                ["traceId"] = httpContext.TraceIdentifier
            }
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Write the ProblemDetails response
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        // Return true to indicate the exception was handled
        return true;
    }
}