# RabbitMQ Logging Implementation

## Overview
This implementation provides two approaches for sending Serilog logs to RabbitMQ:

1. **Custom Sink** (Default) - Sends logs in a specific array format: `["applicationName", "clientDateTime", "logMessage"]`
2. **Standard Sink** - Uses the Serilog.Sinks.RabbitMQ package with JSON object format

## Configuration

### Environment Variables
The application name is configured via the `LOG_APPLICATION_NAME` environment variable in `launchSettings.json`:

```json
"environmentVariables": {
  "ASPNETCORE_ENVIRONMENT": "Development",
  "LOG_APPLICATION_NAME": "ApiWithLog"
}
```

### appsettings.json
RabbitMQ settings are configured in `appsettings.json`:

```json
{
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "QueueName": "application-logs",
    "MinimumLevel": "Information",
    "Enabled": true,
    "UseCustomSink": true
  }
}
```

### Configuration Options

- **HostName**: RabbitMQ server hostname (default: "localhost")
- **Port**: RabbitMQ port (default: 5672)
- **UserName**: RabbitMQ username (default: "guest")
- **Password**: RabbitMQ password (default: "guest")
- **QueueName**: Queue name where logs will be sent (default: "application-logs")
- **MinimumLevel**: Minimum log level to send to RabbitMQ - Options: Debug, Information, Warning, Error, Fatal (default: "Information")
- **Enabled**: Enable/disable RabbitMQ logging (default: true)
- **UseCustomSink**: Switch between custom array format (true) and standard JSON format (false) (default: true)

## Message Formats

### Custom Sink Format (UseCustomSink: true)
Messages are sent as JSON arrays:
```json
["ApiWithLog", "2026-03-18T10:30:45.1234567-03:00", "WeatherForecast endpoint called"]
```

Format: `[applicationName, clientDateTime, logMessage]`
- `applicationName`: From LOG_APPLICATION_NAME environment variable
- `clientDateTime`: ISO 8601 formatted timestamp
- `logMessage`: Fully formatted log message with all properties

### Standard Sink Format (UseCustomSink: false)
Messages are sent as JSON objects with all Serilog properties:
```json
{
  "Timestamp": "2026-03-18T10:30:45.1234567-03:00",
  "Level": "Information",
  "MessageTemplate": "WeatherForecast endpoint called",
  "Properties": {...}
}
```

## Testing

### Prerequisites
1. RabbitMQ server running on localhost:5672
2. Default guest/guest credentials enabled

### Start RabbitMQ (Docker)
```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

### Run the Application
```bash
dotnet run --project src/ApiWithLog/ApiWithLog.csproj
```

### View Messages
1. Access RabbitMQ Management UI: http://localhost:15672
2. Login with guest/guest
3. Navigate to Queues tab
4. Click on "application-logs" queue
5. Use "Get Messages" to view logged messages

### Test Endpoints
```bash
# Generate some logs
curl http://localhost:5055/weatherforecast
```

## Code Structure

### Files Created
- `Logging/CustomRabbitMqSink.cs` - Custom Serilog sink with array format
- `Logging/RabbitMqSinkExtensions.cs` - Extension methods for custom sink
- `Logging/RabbitMqConfiguration.cs` - Configuration model

### Modified Files
- `Program.cs` - Serilog configuration with RabbitMQ sinks
- `appsettings.json` - RabbitMQ configuration section
- `Properties/launchSettings.json` - LOG_APPLICATION_NAME environment variable
- `ApiWithLog.csproj` - Added RabbitMQ packages

## Switching Between Sinks

To switch from custom array format to standard JSON format, simply change the configuration:

```json
{
  "RabbitMq": {
    "UseCustomSink": false
  }
}
```

No code changes required - the application will automatically use the standard Serilog.Sinks.RabbitMQ package.

## Notes

- Logs are also sent to Console regardless of RabbitMQ configuration
- If RabbitMQ connection fails, errors are written to Console.Error
- The custom sink formats the log message with all template properties before sending
- Queue is created automatically if it doesn't exist (durable, non-exclusive, non-auto-delete)
