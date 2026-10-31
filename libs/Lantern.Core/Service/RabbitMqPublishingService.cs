using RabbitMQ.Client;
using Lantern.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Lantern.Core.Service;

internal sealed class RabbitMqPublishingService : IPublishingService
{
    private readonly IJsonService json;
    private readonly Uri url;

    public RabbitMqPublishingService(IOptions<BrokerOptions> options, IJsonService json)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.json = json;
        url = new Uri(options.Value.Url);
    }

    public async Task PublishAsync<T>(string exchange, string key, T message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await using var current = await new ConnectionFactory { Uri = url }.CreateConnectionAsync(cancellationToken);

        await using var channel = await current.CreateChannelAsync(
            new CreateChannelOptions(true, true)
            , cancellationToken);

        await channel.BasicPublishAsync(
            exchange,
            key,
            mandatory: false,
            new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent },
            System.Text.Encoding.UTF8.GetBytes(json.Serialize(message)),
            cancellationToken
        );
    }
}
