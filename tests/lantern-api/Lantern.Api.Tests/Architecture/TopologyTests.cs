using System.Text.Json;
using Lantern.Core.Constants;

namespace Lantern.Api.Tests.Architecture;

public sealed class TopologyTests
{
    [Fact]
    public void EveryActionType_HasAQueueNamedAfterItBoundToTheActionsExchangeByItsWireName()
    {
        using var topology = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "topology.json")));
        var queues = topology.RootElement.GetProperty("queues").EnumerateArray().Select(queue => queue.GetProperty("name").GetString()).ToHashSet();
        var bindings = topology
            .RootElement.GetProperty("bindings")
            .EnumerateArray()
            .Select(binding => (binding.GetProperty("source").GetString(), binding.GetProperty("destination").GetString(), binding.GetProperty("routing_key").GetString()))
            .ToHashSet();

        Assert.All(
            Enum.GetValues<ActionType>(),
            type =>
            {
                Assert.Contains(type.WireName(), queues);
                Assert.Contains(("lantern.actions", type.WireName(), type.WireName()), bindings);
            }
        );
    }

    [Fact]
    public void UnroutableKeysAndDeadLettersBothEndInTheParkingQueue()
    {
        using var topology = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "topology.json")));
        var exchanges = topology.RootElement.GetProperty("exchanges").EnumerateArray().ToDictionary(e => e.GetProperty("name").GetString());

        Assert.Equal("lantern.parking", exchanges["lantern.actions"].GetProperty("arguments").GetProperty("alternate-exchange").GetString());
        Assert.All(
            topology.RootElement.GetProperty("queues").EnumerateArray().Where(queue => queue.GetProperty("name").GetString() != "parking"),
            queue => Assert.Equal("lantern.parking", queue.GetProperty("arguments").GetProperty("x-dead-letter-exchange").GetString())
        );
    }
}
