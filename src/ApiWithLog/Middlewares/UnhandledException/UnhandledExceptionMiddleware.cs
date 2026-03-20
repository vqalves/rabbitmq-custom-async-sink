namespace ApiWithLog.Middlewares;

public class UnhandledExceptionMiddleware
{
    // TODO: Implement code that intercept unhandled exceptions inside the system
    // Those exceptions should be logged via ILogger
    // Then force a response of ProblemDetails with "internal server error"
    // Generate a GUID that is returned in the ProblemDetails and is also sent to the Error log
}