using System.Diagnostics;
using Lantern.Api.Services;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Actions;
using Lantern.Core.Configuration;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Lantern.Api.Tests.Actions;

public sealed class PendingActionResenderTests
{
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string TraceParent = $"00-{TraceId}-b7ad6b7169203331-01";

    [Theory]
    [InlineData(TraceParent, true)]
    [InlineData(null, false)]
    public async Task RunOnceAsync_StartsItsOwnTrace_LinkedToTheRequestThatRecordedTheAction(string traceParent, bool linked)
    {
        var message = new ActionMessage("id", ActionType.RemoveWorkspace, "{}", traceParent);
        var ledger = new Mock<IActionLedger>();
        ledger.Setup(l => l.UnsentSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync([message]);
        var started = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PendingActionResender.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = started.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var resender = new PendingActionResender(
            ledger.Object,
            Mock.Of<IActionPublisher>(),
            Options.Create(new ActionOptions { Exchange = "exchange", ResendAfter = TimeSpan.FromMinutes(5) }),
            TimeProvider.System,
            NullLogger<PendingActionResender>.Instance
        );

        await resender.RunOnceAsync(CancellationToken.None);

        var activity = Assert.Single(started);
        Assert.Equal(linked, activity.Links.Any(link => link.Context.TraceId.ToString() == TraceId));
        Assert.NotEqual(TraceId, activity.TraceId.ToString());
    }

    [Fact]
    public async Task RunOnceAsync_RemovesRowsASubscriberFinishedOnceTheyAreOlderThanDoneExpiry_ThenResendsTheRest()
    {
        var clock = new TestClock();
        var ledger = new Mock<IActionLedger>();
        ledger.Setup(l => l.UnsentSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var resender = new PendingActionResender(
            ledger.Object,
            Mock.Of<IActionPublisher>(),
            Options.Create(new ActionOptions { Exchange = "exchange", ResendAfter = TimeSpan.FromMinutes(5), DoneExpiry = TimeSpan.FromHours(12) }),
            clock,
            NullLogger<PendingActionResender>.Instance
        );

        await resender.RunOnceAsync(CancellationToken.None);

        ledger.Verify(l => l.ExpireDoneAsync(clock.GetUtcNow() - TimeSpan.FromHours(12), It.IsAny<CancellationToken>()), Times.Once);
    }
}
