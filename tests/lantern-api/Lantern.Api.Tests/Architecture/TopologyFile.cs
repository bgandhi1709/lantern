using System.Text.Json.Serialization;

namespace Lantern.Api.Tests.Architecture;

public sealed class TopologyFile
{
    public List<TopologyEntity> Exchanges { get; set; } = [];

    public List<TopologyEntity> Queues { get; set; } = [];

    public List<TopologyBinding> Bindings { get; set; } = [];
}

public sealed class TopologyEntity
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, string> Arguments { get; set; } = [];
}

public sealed class TopologyBinding
{
    public string Source { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    [JsonPropertyName("routing_key")]
    public string RoutingKey { get; set; } = string.Empty;
}
