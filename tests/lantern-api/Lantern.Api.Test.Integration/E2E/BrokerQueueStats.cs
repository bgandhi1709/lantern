using System.Text.Json.Serialization;

namespace Lantern.Api.Test.Integration.E2E;

public sealed class BrokerQueueStats
{
    [JsonPropertyName("publish")]
    public int Published { get; set; }
}
