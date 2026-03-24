namespace ApiWithLogger.Middlewares.RegisterResponseRequest;

public class RegisterResponseRequestMiddlewareData
{
    public string URL { get; }
    public string QueryString { get; }
    public long Duration { get; }
    public string Response { get; }
    public string Request { get; }

    public RegisterResponseRequestMiddlewareData(string url, string queryString, long duration, string response, string request)
    {
        URL = url;
        QueryString = queryString;
        Duration = duration;
        Response = response;
        Request = request;
    }
}