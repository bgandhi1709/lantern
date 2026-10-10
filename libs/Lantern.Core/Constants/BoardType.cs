using System.Text.Json.Serialization;

namespace Lantern.Core.Constants;

// Numbered from 1 so an unset value (0) is never a Board: a Parent has to pick one. The wire names are part of the API
// contract and of the stored row's meaning: never rename them once released.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BoardType
{
    [JsonStringEnumMemberName("cbse")]
    Cbse = 1,

    [JsonStringEnumMemberName("ssc")]
    Ssc = 2,
}
