namespace Lantern.Core.Service;

/// <summary>Publishes any model as JSON to an exchange under a routing key. It knows nothing about what it carries.</summary>
public interface IPublishingService
{
    Task PublishAsync<T>(string exchange, string key, T message, CancellationToken cancellationToken);
}
