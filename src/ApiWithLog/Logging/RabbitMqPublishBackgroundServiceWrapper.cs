using ILogger = Serilog.ILogger;

namespace ApiWithLog.Logging;

/// <summary>Offers a generic interface to bypass the only-one-instance-per-type HostedService</summary>
public class RabbitMqPublishBackgroundServiceWrapper<T> : RabbitMqPublishBackgroundService
{
    public RabbitMqPublishBackgroundServiceWrapper(
        string serviceName, 
        RabbitMqBufferQueue queue, 
        RabbitMqConfiguration rabbitMqConfiguration, 
        ILogger logger) : base(serviceName, queue, rabbitMqConfiguration, logger)
    {
    }
}