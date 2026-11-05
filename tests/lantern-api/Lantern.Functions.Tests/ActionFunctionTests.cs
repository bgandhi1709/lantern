using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Lantern.Functions.Tests.Infrastructure;
using Moq;

namespace Lantern.Functions.Tests;

public sealed class ActionFunctionTests
{
    private readonly Mock<IActionLedger> ledger = new();
    private readonly RecordingFunction function;

    public ActionFunctionTests() => function = new RecordingFunction(ledger.Object);

    [Fact]
    public async Task RunAsync_APendingAction_RunsTheWork_ThenRecordsItsDoneMarker()
    {
        ledger.Setup(l => l.IsPendingAsync(ActionType.RemoveWorkspace, "id", "recording", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await function.RunAsync(Body("{\"familyId\":\"x\"}"), CancellationToken.None);

        Assert.Equal(1, function.Handled);
        ledger.Verify(l => l.MarkDoneAsync(ActionType.RemoveWorkspace, "id", "recording", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_AnActionWhoseRowIsGone_DoesNothing()
    {
        await function.RunAsync(Body("{}"), CancellationToken.None);

        Assert.Equal(0, function.Handled);
        ledger.Verify(l => l.MarkDoneAsync(It.IsAny<ActionType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"id\":\"id\",\"type\":\"mystery\",\"payload\":\"{}\"}")]
    public async Task RunAsync_ABodyThatIsNotAnAction_Throws_SoTheBrokerParksIt(string body) =>
        await Assert.ThrowsAnyAsync<JsonException>(() => function.RunAsync(body, CancellationToken.None));

    [Fact]
    public void EverySubscriberFunction_HasItsOwnName()
    {
        var functions = typeof(RemoveWorkspaceFunction)
            .Assembly.GetTypes()
            .Where(type => !type.IsAbstract && Inherits(type, typeof(ActionFunction<>)))
            .ToList();

        var names = functions.Select(NameOf).ToList();

        Assert.True(functions.Count >= 3);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    private static bool Inherits(Type type, Type definition) =>
        type.BaseType is { } parent && ((parent.IsGenericType && parent.GetGenericTypeDefinition() == definition) || Inherits(parent, definition));

    private static string NameOf(Type function)
    {
        var subscriber = function.GetProperty("Subscriber", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (string)subscriber.GetValue(RuntimeHelpers.GetUninitializedObject(function))!;
    }

    private static string Body(string payload) =>
        new JsonService().Serialize(new ActionMessage("id", ActionType.RemoveWorkspace, payload));
}
