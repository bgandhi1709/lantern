using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Lantern.Core.Actions;
using Lantern.Functions.Handler;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Lantern.Functions.Tests;

public sealed class ActionFunctionTests
{
    private static readonly ActionMessage Action = new("id", ActionType.RemoveWorkspace, "{}");

    private readonly Mock<IActionDispatcher> dispatcher = new();
    private readonly Mock<ServiceBusMessageActions> actions = new();

    private ActionFunction Function => new(dispatcher.Object, NullLogger<ActionFunction>.Instance);

    [Fact]
    public async Task RunAsync_WhenTheHandlerSucceeds_CompletesTheMessage()
    {
        var message = Message(JsonSerializer.Serialize(Action, JsonSerializerOptions.Web));

        await Function.RunAsync(message, actions.Object, CancellationToken.None);

        dispatcher.Verify(d => d.DispatchAsync(Action, It.IsAny<CancellationToken>()), Times.Once);
        actions.Verify(a => a.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenTheHandlerFails_SettlesNothing_SoTheLockExpiryRedeliversIt()
    {
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<ActionMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob down"));
        var message = Message(JsonSerializer.Serialize(Action, JsonSerializerOptions.Web));

        await Function.RunAsync(message, actions.Object, CancellationToken.None);

        actions.Verify(a => a.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        actions.Verify(
            a => a.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        actions.Verify(a => a.AbandonMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_UnknownType_DeadLettersTheMessage()
    {
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<ActionMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnknownActionException(ActionType.RemoveWorkspace));
        var message = Message(JsonSerializer.Serialize(Action, JsonSerializerOptions.Web));

        await Function.RunAsync(message, actions.Object, CancellationToken.None);

        actions.Verify(
            a => a.DeadLetterMessageAsync(message, It.IsAny<Dictionary<string, object>>(), "Rejected", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        actions.Verify(a => a.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"id\":\"id\",\"type\":\"mystery\",\"payload\":\"{}\"}")]
    public async Task RunAsync_BodyThatIsNotAnAction_DeadLettersWithoutDispatching(string body)
    {
        var message = Message(body);

        await Function.RunAsync(message, actions.Object, CancellationToken.None);

        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<ActionMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        actions.Verify(
            a => a.DeadLetterMessageAsync(message, It.IsAny<Dictionary<string, object>>(), "Rejected", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    private static ServiceBusReceivedMessage Message(string body) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(body), messageId: "id", deliveryCount: 1);
}
