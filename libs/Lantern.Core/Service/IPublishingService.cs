namespace Lantern.Core.Service;

public interface IPublishingService
{
    Task PublishAsync<T>(string exchange, string key, T message, CancellationToken cancellationToken);
}
