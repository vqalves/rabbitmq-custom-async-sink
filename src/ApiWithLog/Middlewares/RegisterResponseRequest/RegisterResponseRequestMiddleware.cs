using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Text;

namespace ApiWithLogger.Middlewares.RegisterResponseRequest;

public class RegisterResponseRequestMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RegisterResponseRequestMiddlewareDependencies _dependencies;
    private readonly List<string> IgnoredPathes;

    public RegisterResponseRequestMiddleware(
        RequestDelegate next, 
        RegisterResponseRequestMiddlewareDependencies dependencies)
    {
        _next = next;
        _dependencies = dependencies;

        IgnoredPathes = new()
        {
            "/health",
            "/swagger"
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.HasValue)
        {
            if(IgnoredPathes.Any(ignoredPath => context.Request.Path.Value.Contains(ignoredPath, StringComparison.InvariantCultureIgnoreCase)))
            {
                await _next(context);
                return;
            }
        }

        Stopwatch st = Stopwatch.StartNew();

        // Enable request buffering to allow multiple reads of the request body
        context.Request.EnableBuffering();

        // Read the request body
        var requestBody = await ReadRequestBodyAsync(context.Request);

        // Capture the full URL
        var url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";
        var queryString = $"{context.Request.QueryString}";

        // Store the original response body stream
        var originalResponseBody = context.Response.Body;

        // Create a new memory stream to capture the response
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            // Execute the next middleware in the pipeline
            await _next(context);

            // Read the response body
            var responseBody = await ReadResponseBodyAsync(context.Response);

            // Create the object with url, request, and response
            var capturedData = new RegisterResponseRequestMiddlewareData
            (
                url: url,
                queryString: queryString,
                duration: st.ElapsedMilliseconds,
                request: requestBody,
                response: responseBody
            );

            _dependencies.Register(capturedData);
        }
        finally
        {
            // Copy the response back to the original stream
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalResponseBody);
        }
    }

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.Body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(
            request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();

        // Reset the request body stream position for subsequent reads
        request.Body.Seek(0, SeekOrigin.Begin);

        return body;
    }

    private async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(
            response.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();

        response.Body.Seek(0, SeekOrigin.Begin);

        return body;
    }
}