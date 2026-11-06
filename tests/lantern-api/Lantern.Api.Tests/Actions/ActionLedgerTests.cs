using Azure.Data.Tables;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Repository;
using Lantern.Repository.Entities;
using Lantern.Repository.UnitOfWork;

namespace Lantern.Api.Tests.Actions;

[Collection(ApiCollection.Name)]
// Real Azurite: a missing row and a repeated add are table behaviour a fake would hide.
public sealed class ActionLedgerTests(AzuriteFixture azurite)
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly TableClient table = new TableServiceClient(azurite.ConnectionString).GetTableClient(
        $"actions{Guid.NewGuid():N}"
    );

    private ActionLedger Ledger => new(new UnitOfWork<ActionEntity>(table, createTable: true));

    [Fact]
    public async Task RecordAsync_RepeatingAnIdChangesNothing()
    {
        await Ledger.RecordAsync(Message("a_b", "{\"familyId\":1}"), T0, CancellationToken.None);
        await Ledger.RecordAsync(Message("a_b", "{\"other\":2}"), T0.AddHours(1), CancellationToken.None);

        var row = Assert.Single(await table.QueryAsync<TableEntity>().ToListAsync());
        Assert.Equal(("RemoveWorkspace", "a_b", "{\"familyId\":1}", T0), (row.PartitionKey, row.RowKey, row.GetString("Payload"), row.GetDateTimeOffset("LastSentAt")));
    }

    [Fact]
    public async Task RecordAsync_KeepsTheTraceParent_ForTheResend()
    {
        const string traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

        await Ledger.RecordAsync(Message("t") with { TraceParent = traceParent }, T0, CancellationToken.None);

        var stale = Assert.Single(await Resendable(T0, T0));
        Assert.Equal(traceParent, stale.TraceParent);
    }

    [Fact]
    public async Task IsPendingAsync_IsTrueForASubscriberFromRecordUntilItsOwnMarker_AndMarkingIsRepeatable()
    {
        Assert.False(await IsPendingAsync("x", "plan"));

        await Ledger.RecordAsync(Message("x"), T0, CancellationToken.None);
        Assert.True(await IsPendingAsync("x", "plan"));
        Assert.True(await IsPendingAsync("x", "remove-workspace"));

        await Ledger.MarkDoneAsync(ActionType.RemoveWorkspace, "x", "remove-workspace", CancellationToken.None);
        await Ledger.MarkDoneAsync(ActionType.RemoveWorkspace, "x", "remove-workspace", CancellationToken.None);

        Assert.False(await IsPendingAsync("x", "remove-workspace"));
        Assert.True(await IsPendingAsync("x", "plan"));
    }

    [Fact]
    public async Task MarkDoneAsync_TwoSubscribersAtOnce_BothMarkersLand()
    {
        await Ledger.RecordAsync(Message("x"), T0, CancellationToken.None);

        await Task.WhenAll(
            Enumerable
                .Range(0, 6)
                .Select(i => Ledger.MarkDoneAsync(ActionType.RemoveWorkspace, "x", $"subscriber-{i}", CancellationToken.None))
        );

        for (var i = 0; i < 6; i++)
        {
            Assert.False(await IsPendingAsync("x", $"subscriber-{i}"));
        }
    }

    [Fact]
    public async Task MarkDoneAsync_ForARowThatIsGone_IsNotAnError()
    {
        await table.CreateIfNotExistsAsync();

        await Ledger.MarkDoneAsync(ActionType.RemoveWorkspace, "gone", "remove-workspace", CancellationToken.None);
    }

    [Fact]
    public async Task ResendableAsync_NeverReturnsARowASubscriberFinished()
    {
        await Ledger.RecordAsync(Message("done"), T0, CancellationToken.None);
        await Ledger.RecordAsync(Message("nobody"), T0, CancellationToken.None);
        await Ledger.MarkDoneAsync(ActionType.RemoveWorkspace, "done", "remove-workspace", CancellationToken.None);

        var stale = await Resendable(T0.AddMinutes(5), T0.AddMinutes(5));

        Assert.Equal([Message("nobody")], stale);
    }

    [Fact]
    public async Task ExpireDoneAsync_RemovesOnlyRowsASubscriberFinishedThatWereRecordedBeforeTheCutoff()
    {
        await Ledger.RecordAsync(Message("old-done"), T0, CancellationToken.None);
        await Ledger.RecordAsync(Message("old-nobody"), T0, CancellationToken.None);
        await Ledger.RecordAsync(Message("recent-done"), T0.AddHours(2), CancellationToken.None);
        await Ledger.MarkDoneAsync(ActionType.RemoveWorkspace, "old-done", "remove-workspace", CancellationToken.None);
        await Ledger.MarkDoneAsync(ActionType.RemoveWorkspace, "recent-done", "remove-workspace", CancellationToken.None);

        await Ledger.ExpireDoneAsync(T0.AddHours(1), CancellationToken.None);
        await Ledger.ExpireDoneAsync(T0.AddHours(1), CancellationToken.None);

        var kept = await table.QueryAsync<TableEntity>().Select(row => row.RowKey).OrderBy(key => key).ToListAsync();
        Assert.Equal(["old-nobody", "recent-done"], kept);
    }

    private Task<IReadOnlyList<ActionMessage>> Resendable(DateTimeOffset unconfirmedBefore, DateTimeOffset confirmedBefore) =>
        Ledger.ResendableAsync(unconfirmedBefore, confirmedBefore, CancellationToken.None);

    private async Task<bool> IsPendingAsync(string id, string subscriber) =>
        await Ledger.IsPendingAsync(ActionType.RemoveWorkspace, id, subscriber, CancellationToken.None);

    [Fact]
    public async Task ResendableAsync_ReturnsOnlyActionsNotSentSinceTheCutoff_AndMarkSentMovesThemOut()
    {
        await Ledger.RecordAsync(Message("old"), T0, CancellationToken.None);
        await Ledger.RecordAsync(Message("new"), T0.AddMinutes(10), CancellationToken.None);

        var stale = await Resendable(T0.AddMinutes(5), T0.AddMinutes(5));
        await Ledger.MarkSentAsync(ActionType.RemoveWorkspace, "old", T0.AddMinutes(11), CancellationToken.None);
        var after = await Resendable(T0.AddMinutes(5), T0.AddMinutes(5));

        Assert.Equal([Message("old")], stale);
        Assert.Empty(after);
    }

    [Fact]
    public async Task ResendableAsync_AnUnconfirmedRowWaitsTheShortThreshold_AConfirmedRowWaitsTheLongOne()
    {
        await Ledger.RecordAsync(Message("unconfirmed"), T0, CancellationToken.None);
        await Ledger.RecordAsync(Message("confirmed"), T0, CancellationToken.None);
        await Ledger.MarkSentAsync(ActionType.RemoveWorkspace, "confirmed", T0.AddMinutes(1), CancellationToken.None);

        var tenMinutesIn = await Resendable(T0.AddMinutes(5), T0.AddMinutes(-50));
        var twoHoursIn = await Resendable(T0.AddMinutes(115), T0.AddMinutes(60));

        Assert.Equal([Message("unconfirmed")], tenMinutesIn);
        Assert.Equal(["confirmed", "unconfirmed"], twoHoursIn.Select(message => message.Id).Order());
    }

    [Fact]
    public async Task ARowWrittenBeforeDoneMarkersExisted_IsPendingResendableAndFinishable_ButNeverExpires()
    {
        await table.CreateIfNotExistsAsync();
        await table.AddEntityAsync(new TableEntity(nameof(ActionType.RemoveWorkspace), "legacy") { ["Payload"] = "{}", ["LastSentAt"] = T0 });

        Assert.True(await IsPendingAsync("legacy", "remove-workspace"));
        Assert.Equal([Message("legacy")], await Resendable(T0.AddMinutes(5), T0.AddMinutes(5)));

        await Ledger.MarkDoneAsync(ActionType.RemoveWorkspace, "legacy", "remove-workspace", CancellationToken.None);
        await Ledger.ExpireDoneAsync(T0.AddDays(30), CancellationToken.None);

        Assert.False(await IsPendingAsync("legacy", "remove-workspace"));
        Assert.Single(await table.QueryAsync<TableEntity>().ToListAsync());
    }

    [Fact]
    public async Task MarkSentAsync_ForAnActionAlreadyCompleted_IsNotAnError()
    {
        await table.CreateIfNotExistsAsync();

        await Ledger.MarkSentAsync(ActionType.RemoveWorkspace, "gone", T0, CancellationToken.None);
    }

    private static ActionMessage Message(string id, string payload = "{}") => new(id, ActionType.RemoveWorkspace, payload);
}
