using System.Text.Json.Serialization;

namespace Lantern.Api.Test.Integration.E2E;

public sealed class BrokerQueueState
{
    [JsonPropertyName("message_stats")]
    public BrokerQueueStats Stats { get; set; } = new();
}
