using System.Text;

namespace ApiWithLog.Middlewares.RegisterResponseRequest;

public class RegisterResponseRequestMiddlewareData
{
    public string URL { get; set; }
    public string Response { get; set; }
    public string Request { get; set; }

    public RegisterResponseRequestMiddlewareData(string url, string response, string request)
    {
        URL = url;
        Response = response;
        Request = request;
    }
}