using Lantern.Core.Constants;
using Lantern.Core.Service;

namespace Lantern.Api.Tests.Architecture;

public sealed class TopologyTests
{
    private const string ActionsExchange = "lantern.actions";
    private const string ParkingExchange = "lantern.parking";
    private const string ParkingQueue = "parking";

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
    public void UnroutableKeysAndDeadLettersBothEndInTheParkingQueue()
    {
        var topology = Load();
        var actions = Assert.Single(topology.Exchanges, exchange => exchange.Name == ActionsExchange);

        Assert.Equal(ParkingExchange, actions.Arguments["alternate-exchange"]);
        Assert.Contains(topology.Bindings, binding => binding.Source == ParkingExchange && binding.Destination == ParkingQueue);
        Assert.All(
            topology.Queues.Where(queue => queue.Name != ParkingQueue),
            queue => Assert.Equal(ParkingExchange, queue.Arguments["x-dead-letter-exchange"])
        );
    }

    private static TopologyFile Load() =>
        Json.Deserialize<TopologyFile>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "topology.json")));
}
