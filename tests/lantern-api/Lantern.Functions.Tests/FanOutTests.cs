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
    private const string Outsider = "a-third-subscriber";

    private readonly FunctionsHost host = new(azurite.ConnectionString);

    private IActionLedger Ledger => host.Services.GetRequiredService<IActionLedger>();

    [Fact]
    public async Task TwoSubscribersOnOneKey_EachWorkOnce_AndALateDeliveryAfterExpiryDoesNothing()
    {
        var message = new ActionMessage(Guid.NewGuid().ToString("N"), ActionType.RemoveWorkspace, "{}");
        var body = new JsonService().Serialize(message);
        var plan = new PlanTestFunction(Ledger);
        var check = new CheckTestFunction(Ledger);
        await Ledger.RecordAsync(message, host.Clock.GetUtcNow(), CancellationToken.None);

        foreach (var _ in Enumerable.Range(0, 2))
        {
            await plan.RunAsync(body);
            await check.RunAsync(body);
        }

        Assert.Equal("plan-test", plan.Name);
        Assert.Equal("check-test", check.Name);
        Assert.Equal(1, plan.Handled);
        Assert.Equal(1, check.Handled);
        Assert.True(await Ledger.IsPendingAsync(message.Type, message.Id, Outsider, CancellationToken.None));

        await Ledger.ExpireDoneAsync(host.Clock.GetUtcNow().AddHours(1), CancellationToken.None);
        await plan.RunAsync(body);
        await check.RunAsync(body);

        Assert.Equal(1, plan.Handled);
        Assert.Equal(1, check.Handled);
        Assert.False(await Ledger.IsPendingAsync(message.Type, message.Id, Outsider, CancellationToken.None));
    }

    [Fact]
    public async Task AnActionNobodyFinished_IsPendingForEverySubscriber_AndIsResent()
    {
        var message = new ActionMessage(Guid.NewGuid().ToString("N"), ActionType.RemoveWorkspace, "{}");
        await Ledger.RecordAsync(message, host.Clock.GetUtcNow(), CancellationToken.None);
        var later = host.Clock.GetUtcNow().AddMinutes(5);

        Assert.Contains(message, await Ledger.ResendableAsync(later, later, CancellationToken.None));
        Assert.True(await Ledger.IsPendingAsync(message.Type, message.Id, "plan-test", CancellationToken.None));
        Assert.True(await Ledger.IsPendingAsync(message.Type, message.Id, "check-test", CancellationToken.None));
    }

    public void Dispose() => host.Dispose();

    private sealed class PlanTestFunction(IActionLedger ledger) : RecordingFunction(ledger);

    private sealed class CheckTestFunction(IActionLedger ledger) : RecordingFunction(ledger);
}
