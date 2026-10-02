using System.Text.Json;
using Lantern.Core.Actions;
using Lantern.Functions.Handler;
using Moq;

namespace Lantern.Functions.Tests;

public sealed class ActionDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_CallsTheHandlerOfTheMessagesType()
    {
        var delete = Handler(ActionType.RemoveWorkspace);
        var message = new ActionMessage("id", ActionType.RemoveWorkspace, "{}");

        await new ActionDispatcher([delete.Object]).DispatchAsync(message, CancellationToken.None);

        delete.Verify(h => h.HandleAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_TypeWithNoHandler_Throws() =>
        await Assert.ThrowsAsync<UnknownActionException>(() =>
            new ActionDispatcher([]).DispatchAsync(new ActionMessage("id", ActionType.RemoveWorkspace, "{}"), CancellationToken.None)
        );

    [Fact]
    public void Constructor_TwoHandlersForOneType_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new ActionDispatcher([Handler(ActionType.RemoveWorkspace).Object, Handler(ActionType.RemoveWorkspace).Object])
        );

    // The queue carries the wire name, so a message stays readable if the enum member is ever renamed.
    [Fact]
    public void ActionMessage_OnTheWire_NamesTheTypeInKebabCase()
    {
        var json = JsonSerializer.Serialize(new ActionMessage("id", ActionType.RemoveWorkspace, "{}"), JsonSerializerOptions.Web);

        Assert.Contains("\"type\":\"remove-workspace\"", json, StringComparison.Ordinal);
        Assert.Equal(ActionType.RemoveWorkspace, JsonSerializer.Deserialize<ActionMessage>(json, JsonSerializerOptions.Web)!.Type);
    }

    private static Mock<IActionHandler> Handler(ActionType type)
    {
        var handler = new Mock<IActionHandler>();
        handler.SetupGet(h => h.Type).Returns(type);

        return handler;
    }
}
