using System.Text.Json;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Moq;

namespace Lantern.Functions.Tests;

public sealed class ActionFunctionTests
{
    private readonly Mock<IActionLedger> ledger = new();
    private readonly RecordingFunction function;

    public ActionFunctionTests() => function = new RecordingFunction(ledger.Object);

    [Fact]
    public async Task RunAsync_APendingAction_RunsTheWork_ThenCompletesTheLedgerRow()
    {
        ledger.Setup(l => l.IsPendingAsync(ActionType.RemoveWorkspace, "id", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await function.RunAsync(Body("{\"familyId\":\"x\"}"), CancellationToken.None);

        Assert.Equal(1, function.Handled);
        ledger.Verify(l => l.CompleteAsync(ActionType.RemoveWorkspace, "id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_AnActionWhoseRowIsGone_DoesNothing()
    {
        await function.RunAsync(Body("{}"), CancellationToken.None);

        Assert.Equal(0, function.Handled);
        ledger.Verify(l => l.CompleteAsync(It.IsAny<ActionType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"id\":\"id\",\"type\":\"mystery\",\"payload\":\"{}\"}")]
    public async Task RunAsync_ABodyThatIsNotAnAction_Throws_SoTheBrokerParksIt(string body) =>
        await Assert.ThrowsAnyAsync<JsonException>(() => function.RunAsync(body, CancellationToken.None));

    private static string Body(string payload) =>
        new JsonService().Serialize(new ActionMessage("id", ActionType.RemoveWorkspace, payload));

    private sealed class Payload
    {
        public string FamilyId { get; set; } = string.Empty;
    }

    private sealed class RecordingFunction(IActionLedger ledger) : ActionFunction<Payload>(ledger, new JsonService())
    {
        public int Handled { get; private set; }

        public Task RunAsync(string body, CancellationToken cancellationToken) => ExecuteAsync(body, cancellationToken);

        protected override Task HandleAsync(Payload payload, CancellationToken cancellationToken)
        {
            Handled++;
            return Task.CompletedTask;
        }
    }
}
