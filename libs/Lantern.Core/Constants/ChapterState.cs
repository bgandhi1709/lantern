using System.Text.Json.Serialization;

namespace Lantern.Core.Constants;

[JsonConverter(typeof(JsonStringEnumConverter<ChapterState>))]
public enum ChapterState
{
    [JsonStringEnumMemberName("not-started")]
    NotStarted,

    [JsonStringEnumMemberName("planned")]
    Planned,

    [JsonStringEnumMemberName("taught")]
    Taught,

    [JsonStringEnumMemberName("checked")]
    Checked,
}
