using System.Text.Json.Serialization;

namespace Lantern.Core.Constants;

[JsonConverter(typeof(JsonStringEnumConverter<ActionType>))]
public enum ActionType
{
    [JsonStringEnumMemberName("remove-workspace")]
    RemoveWorkspace,

    [JsonStringEnumMemberName("create-workspace")]
    CreateWorkspace,

    [JsonStringEnumMemberName("remove-family")]
    RemoveFamily,
}
