using ApiWithLog.Logging;
using ApiWithLogger.Middlewares.RegisterResponseRequest;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace ApiWithLogger.Middlewares.RegisterResponseRequest;

public class RegisterResponseRequestMessageFormatter : IRabbitMqMessageFormatter<RegisterResponseRequestMiddlewareData>
{
    public byte[] Format(RegisterResponseRequestMiddlewareData data)
    {
        var content = JsonSerializer.Serialize(data);
        var jsonMessage = JsonSerializer.Serialize(content);
        return Encoding.UTF8.GetBytes(jsonMessage);
    }
}