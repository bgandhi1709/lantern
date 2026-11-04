using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Lantern.Functions.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Functions.Tests;

[Collection(FunctionsCollection.Name)]
public sealed class FanOutTests(AzuriteFixture azurite) : IDisposable
{
    private readonly FunctionsHost host = new(azurite.ConnectionString);

    private IActionLedger Ledger => host.Services.GetRequiredService<IActionLedger>();

    [Fact]
    public async Task TwoSubscribersOnOneKey_EachDoTheirWorkOnce_HoweverOftenItIsDelivered_AndALateDeliveryAfterExpiryDoesNothing()
    {
        var json = new JsonService();
        var message = new ActionMessage(Guid.NewGuid().ToString("N"), ActionType.RemoveWorkspace, "{}");
        var body = json.Serialize(message);
        var plan = new PlanTestFunction(Ledger, json);
        var check = new CheckTestFunction(Ledger, json);
        await Ledger.RecordAsync(message, host.Clock.GetUtcNow(), CancellationToken.None);

        foreach (var _ in Enumerable.Range(0, 2))
        {
            await plan.RunAsync(body);
            await check.RunAsync(body);
        }

        Assert.Equal(("plan-test", 1, "check-test", 1), (plan.Subscriber, plan.Handled, check.Subscriber, check.Handled));
        Assert.True(await Ledger.IsPendingAsync(message.Type, message.Id, "a-third-subscriber", CancellationToken.None));

        await Ledger.ExpireDoneAsync(host.Clock.GetUtcNow().AddHours(1), CancellationToken.None);
        await plan.RunAsync(body);
        await check.RunAsync(body);

        Assert.Equal((1, 1), (plan.Handled, check.Handled));
        Assert.False(await Ledger.IsPendingAsync(message.Type, message.Id, "a-third-subscriber", CancellationToken.None));
    }

    [Fact]
    public async Task AnActionNobodyFinished_IsPendingForEverySubscriber_AndIsResent()
    {
        var message = new ActionMessage(Guid.NewGuid().ToString("N"), ActionType.RemoveWorkspace, "{}");
        await Ledger.RecordAsync(message, host.Clock.GetUtcNow(), CancellationToken.None);

        Assert.Contains(message, await Ledger.UnsentSinceAsync(host.Clock.GetUtcNow().AddMinutes(5), CancellationToken.None));
        Assert.True(await Ledger.IsPendingAsync(message.Type, message.Id, "plan-test", CancellationToken.None));
        Assert.True(await Ledger.IsPendingAsync(message.Type, message.Id, "check-test", CancellationToken.None));
    }

    public void Dispose() => host.Dispose();

    private sealed class Payload;

    private sealed class PlanTestFunction(IActionLedger ledger, IJsonService json) : TestFunction(ledger, json);

    private sealed class CheckTestFunction(IActionLedger ledger, IJsonService json) : TestFunction(ledger, json);

    private abstract class TestFunction(IActionLedger ledger, IJsonService json) : ActionFunction<Payload>(ledger, json)
    {
        public int Handled { get; private set; }

        public Task RunAsync(string body) => ExecuteAsync(body, CancellationToken.None);

        protected override Task HandleAsync(Payload payload, CancellationToken cancellationToken)
        {
            Handled++;
            return Task.CompletedTask;
        }
    }
}
