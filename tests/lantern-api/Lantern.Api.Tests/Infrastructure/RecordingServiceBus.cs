using System.Collections.Concurrent;
using Lantern.Core.Actions;
using Lantern.Core.Service;

namespace Lantern.Api.Tests.Infrastructure;

// Stands in for Service Bus: it keeps what the API sent, and the test delivers it to the real dispatcher, which is
// what the Functions app does with each message.
public sealed class RecordingServiceBus : IServiceBusService
{
    public const string Queue = "test-actions";

    private readonly ConcurrentQueue<ActionMessage> sent = new();

    public IReadOnlyList<ActionMessage> Sent => [.. sent];

    public bool Down { get; set; }

    public Task SendAsync<T>(string queueName, T message, CancellationToken cancellationToken)
    {
        if (Down)
        {
            throw new InvalidOperationException("The queue is down.");
        }

        Assert.Equal(Queue, queueName);
        sent.Enqueue(Assert.IsType<ActionMessage>(message));

        return Task.CompletedTask;
    }
}
