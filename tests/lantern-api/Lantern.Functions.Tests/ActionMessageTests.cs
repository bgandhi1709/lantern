using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Service;

namespace Lantern.Functions.Tests;

public sealed class ActionMessageTests
{
    private readonly JsonService json = new();

    [Theory]
    [InlineData(ActionType.RemoveWorkspace, "remove-workspace")]
    [InlineData(ActionType.CreateWorkspace, "create-workspace")]
    [InlineData(ActionType.RemoveFamily, "remove-family")]
    public void OnTheWire_NamesTheTypeInKebabCase_AndReadsBack(ActionType type, string wireName)
    {
        var body = json.Serialize(new ActionMessage("id", type, "{}"));

        Assert.Contains($"\"type\":\"{wireName}\"", body, StringComparison.Ordinal);
        Assert.Equal(type, json.Deserialize<ActionMessage>(body).Type);
    }
}
