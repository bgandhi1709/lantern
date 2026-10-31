using System.Diagnostics;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Actions;
using Lantern.Core.Configuration;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Lantern.Api.Tests.Actions;

public sealed class ActionPublisherTests
{
    private static readonly ActionMessage Message = new("id", ActionType.RemoveWorkspace, "{}");

    private readonly Mock<IPublishingService> publishing = new();
    private readonly Mock<IActionLedger> ledger = new();
    private readonly FixedClock clock = new FixedClock(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));

    private ActionPublisher Publisher => new(publishing.Object, ledger.Object, TestServices.Json, Options.Create(new ActionOptions { Exchange = "exchange" }), clock, NullLogger<ActionPublisher>.Instance);

    [Fact]
    public async Task RecordAsync_WritesTheLedgerRow_WithTheWebJsonPayload()
    {
        var message = await Publisher.RecordAsync(ActionType.RemoveWorkspace, "id", new RemoveWorkspacePayload(Guid.Empty, Guid.Empty), CancellationToken.None);

        Assert.Contains("\"familyId\"", message.Payload, StringComparison.Ordinal);
        ledger.Verify(l => l.RecordAsync(message, clock.GetUtcNow(), It.IsAny<CancellationToken>()), Times.Once);
        publishing.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RecordAsync_WithAnActivityCurrent_StoresItsTraceParentOnTheMessageAndTheLedgerRow()
    {
        using var source = new ActivitySource("test");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("request");

        var message = await Publisher.RecordAsync(ActionType.RemoveWorkspace, "id", new RemoveWorkspacePayload(Guid.Empty, Guid.Empty), CancellationToken.None);

        Assert.StartsWith($"00-{activity!.TraceId}-{activity.SpanId}", message.TraceParent, StringComparison.Ordinal);
        ledger.Verify(l => l.RecordAsync(message, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_PublishesTheMessageToTheExchange_WithTheWireNameAsKey_ThenMarksItSent()
    {
        await Publisher.SendAsync(Message, CancellationToken.None);

        publishing.Verify(s => s.PublishAsync("exchange", "remove-workspace", Message, It.IsAny<CancellationToken>()), Times.Once);
        ledger.Verify(l => l.MarkSentAsync(ActionType.RemoveWorkspace, "id", clock.GetUtcNow(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenTheSendFails_DoesNotThrow_AndDoesNotMarkItSent()
    {
        publishing
            .Setup(s => s.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ActionMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("down"));

        await Publisher.SendAsync(Message, CancellationToken.None);

        ledger.Verify(
            l => l.MarkSentAsync(It.IsAny<ActionType>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SendAsync_WhenCancelled_Throws()
    {
        publishing
            .Setup(s => s.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ActionMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Publisher.SendAsync(Message, CancellationToken.None));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
