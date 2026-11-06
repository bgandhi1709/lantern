using Lantern.Core.Constants;
using Lantern.Core.Service;

namespace Lantern.Api.Tests.Architecture;

public sealed class TopologyTests
{
    private const string ActionsExchange = "lantern.actions";
    private const string ParkingExchange = "lantern.parking";
    private const string ParkingQueue = "parking";
    private const string RetryExchange = "lantern.retry";
    private const string RetryDelayPlaceholder = "{{retryDelayMs}}";

    private static readonly JsonService Json = new();

    [Fact]
    public void EveryActionType_HasAQueueNamedAfterItBoundToTheActionsExchangeByItsWireName()
    {
        var topology = Load();
        var queues = topology.Queues.Select(queue => queue.Name).ToHashSet();
        var bindings = topology.Bindings.Select(binding => (binding.Source, binding.Destination, binding.RoutingKey)).ToHashSet();

        Assert.All(
            Enum.GetValues<ActionType>(),
            type =>
            {
                var wireName = Json.Deserialize<string>(Json.Serialize(type));

                Assert.Contains(wireName, queues);
                Assert.Contains((ActionsExchange, wireName, wireName), bindings);
            }
        );
    }

    [Fact]
    public void UnroutableKeys_EndInTheParkingQueue()
    {
        var topology = Load();
        var actions = Assert.Single(topology.Exchanges, exchange => exchange.Name == ActionsExchange);

        Assert.Equal(ParkingExchange, actions.Arguments["alternate-exchange"]);
        Assert.Contains(topology.Bindings, binding => binding.Source == ParkingExchange && binding.Destination == ParkingQueue);
    }

    [Fact]
    public void EveryWorkQueue_RetriesThroughItsOwnDelayQueueThatReturnsToIt()
    {
        var topology = Load();

        Assert.All(
            Enum.GetValues<ActionType>(),
            type =>
            {
                var work = Json.Deserialize<string>(Json.Serialize(type));
                var delay = $"{work}-delay";
                var workQueue = Assert.Single(topology.Queues, queue => queue.Name == work);
                var delayQueue = Assert.Single(topology.Queues, queue => queue.Name == delay);

                Assert.Equal(RetryExchange, workQueue.Arguments["x-dead-letter-exchange"]);
                Assert.Equal(work, workQueue.Arguments["x-dead-letter-routing-key"]);
                Assert.Contains(topology.Bindings, binding => (binding.Source, binding.Destination, binding.RoutingKey) == (RetryExchange, delay, work));
                Assert.Equal(string.Empty, delayQueue.Arguments["x-dead-letter-exchange"]);
                Assert.Equal(work, delayQueue.Arguments["x-dead-letter-routing-key"]);
                Assert.Equal(RetryDelayPlaceholder, delayQueue.Arguments["x-message-ttl"]);
            }
        );
    }

    private static TopologyFile Load() =>
        Json.Deserialize<TopologyFile>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "topology.json")));
}
