using System.Collections.Concurrent;
using Lantern.Core.Actions;
using Lantern.Core.Service;

namespace Lantern.Api.Tests.Infrastructure;

public sealed class RecordingPublishingService : IPublishingService
{
    public const string Exchange = "test-actions";

    private readonly ConcurrentQueue<ActionMessage> sent = new();

    public IReadOnlyList<ActionMessage> Sent => [.. sent];

    public bool Down { get; set; }

    public Task PublishAsync<T>(string exchange, string key, T message, CancellationToken cancellationToken)
    {
        if (Down)
        {
            throw new InvalidOperationException("The broker is down.");
        }

        Assert.Equal(Exchange, exchange);
        var action = Assert.IsType<ActionMessage>(message);
        Assert.Contains($"\"type\":\"{key}\"", new JsonService().Serialize(action), StringComparison.Ordinal);
        sent.Enqueue(action);

        return Task.CompletedTask;
    }
}
