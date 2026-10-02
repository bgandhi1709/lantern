namespace Lantern.Core.Service;

/// <summary>Sends any model as JSON to a queue. It knows nothing about what it sends.</summary>
public interface IServiceBusService
{
    Task SendAsync<T>(string queueName, T message, CancellationToken cancellationToken);
}
