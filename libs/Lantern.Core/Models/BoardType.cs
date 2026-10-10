using System.Text.Json.Serialization;

namespace Lantern.Core.Models;

// The wire names are part of the API contract and of the stored row's meaning: never rename them once released.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BoardType
{
    [JsonStringEnumMemberName("cbse")]
    Cbse,

    [JsonStringEnumMemberName("ssc")]
    Ssc,
}
