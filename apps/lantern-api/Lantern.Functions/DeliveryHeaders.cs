using System.Text.Json.Serialization;

namespace Lantern.Functions;

internal sealed class DeliveryHeaders
{
    [JsonPropertyName("x-death")]
    public List<DeliveryDeath> Deaths { get; set; }
}
