using System.Text;
using Lantern.Core.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Lantern.Core.Service;

internal sealed class RabbitMqPublishingService(IOptions<BrokerOptions> options, IJsonService json)
    : IPublishingService,
        IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection connection;
    private IChannel channel;

    public async Task PublishAsync<T>(string exchange, string key, T message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var open = await OpenChannelAsync(cancellationToken);

            await open.BasicPublishAsync(
                exchange,
                key,
                mandatory: false,
                new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent },
                Encoding.UTF8.GetBytes(json.Serialize(message)),
                cancellationToken
            );
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        gate.Dispose();
    }

    private async Task<IChannel> OpenChannelAsync(CancellationToken cancellationToken)
    {
        if (channel is { IsOpen: true })
        {
            return channel;
        }

        await CloseAsync();

        connection = await new ConnectionFactory { Uri = new Uri(options.Value.Url) }.CreateConnectionAsync(cancellationToken);
        channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken
        );

        return channel;
    }

    private async Task CloseAsync()
    {
        if (channel is not null)
        {
            await channel.DisposeAsync();
            channel = null;
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
            connection = null;
        }
    }
}
