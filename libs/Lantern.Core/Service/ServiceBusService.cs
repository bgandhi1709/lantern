using System.Collections.Concurrent;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Lantern.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Lantern.Core.Service;

internal sealed class ServiceBusService : IServiceBusService, IAsyncDisposable
{
    private readonly IJsonService json;
    private readonly Lazy<ServiceBusClient> client;
    private readonly ConcurrentDictionary<string, ServiceBusSender> senders = new(StringComparer.Ordinal);

    public ServiceBusService(IOptions<ServiceBusOptions> options, IJsonService json)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.json = json;

        client = new(() =>
            string.IsNullOrWhiteSpace(options.Value.ConnectionString)
                ? new ServiceBusClient(options.Value.FullyQualifiedNamespace, new DefaultAzureCredential())
                : new ServiceBusClient(options.Value.ConnectionString)
        );
    }

    public async Task SendAsync<T>(string queueName, T message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var sender = senders.GetOrAdd(queueName, name => client.Value.CreateSender(name));
        var body = new ServiceBusMessage(BinaryData.FromString(json.Serialize(message)))
        {
            ContentType = "application/json",
        };

        await sender.SendMessageAsync(body, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in senders.Values)
        {
            await sender.DisposeAsync();
        }

        if (client.IsValueCreated)
        {
            await client.Value.DisposeAsync();
        }
    }
}
