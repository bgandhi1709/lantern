using System.Text.Json.Serialization;

namespace Lantern.Api.Test.Integration.E2E;

public sealed class BrokerQueue
{
    [JsonPropertyName("message_stats")]
    public BrokerQueueStats Stats { get; set; } = new();
}

public sealed class BrokerQueueStats
{
    [JsonPropertyName("publish")]
    public int Published { get; set; }
}
