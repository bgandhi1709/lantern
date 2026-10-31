using System.Reflection;
using System.Text.Json.Serialization;

namespace Lantern.Core.Constants;

public static class ActionTypeExtensions
{
    public static string WireName(this ActionType type) =>
        typeof(ActionType).GetField(type.ToString())!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name;
}
