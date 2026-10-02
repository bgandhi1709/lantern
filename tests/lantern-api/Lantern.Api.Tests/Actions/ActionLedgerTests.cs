using Azure.Data.Tables;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Actions;
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

        var stale = Assert.Single(await Ledger.UnsentSinceAsync(T0, CancellationToken.None));
        Assert.Equal(traceParent, stale.TraceParent);
    }

    [Fact]
    public async Task IsPendingAsync_IsTrueFromRecordToComplete_AndCompleteIsRepeatable()
    {
        Assert.False(await Ledger.IsPendingAsync(ActionType.RemoveWorkspace, "x", CancellationToken.None));

        await Ledger.RecordAsync(Message("x"), T0, CancellationToken.None);
        Assert.True(await Ledger.IsPendingAsync(ActionType.RemoveWorkspace, "x", CancellationToken.None));

        await Ledger.CompleteAsync(ActionType.RemoveWorkspace, "x", CancellationToken.None);
        await Ledger.CompleteAsync(ActionType.RemoveWorkspace, "x", CancellationToken.None);
        Assert.False(await Ledger.IsPendingAsync(ActionType.RemoveWorkspace, "x", CancellationToken.None));
    }

    [Fact]
    public async Task UnsentSinceAsync_ReturnsOnlyActionsNotSentSinceTheCutoff_AndMarkSentMovesThemOut()
    {
        await Ledger.RecordAsync(Message("old"), T0, CancellationToken.None);
        await Ledger.RecordAsync(Message("new"), T0.AddMinutes(10), CancellationToken.None);

        var stale = await Ledger.UnsentSinceAsync(T0.AddMinutes(5), CancellationToken.None);
        await Ledger.MarkSentAsync(ActionType.RemoveWorkspace, "old", T0.AddMinutes(11), CancellationToken.None);
        var after = await Ledger.UnsentSinceAsync(T0.AddMinutes(5), CancellationToken.None);

        Assert.Equal([Message("old")], stale);
        Assert.Empty(after);
    }

    [Fact]
    public async Task MarkSentAsync_ForAnActionAlreadyCompleted_IsNotAnError()
    {
        await table.CreateIfNotExistsAsync();

        await Ledger.MarkSentAsync(ActionType.RemoveWorkspace, "gone", T0, CancellationToken.None);
    }

    private static ActionMessage Message(string id, string payload = "{}") => new(id, ActionType.RemoveWorkspace, payload);
}
