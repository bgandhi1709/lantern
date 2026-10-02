using Azure;
using Azure.Data.Tables;
using Lantern.Core.Actions;
using Lantern.Core.Repository;
using Lantern.Repository.Entities;
using Lantern.Repository.UnitOfWork;

namespace Lantern.Repository;

// Partition is the action type and the row key the action id, so a repeat of the same request is the same row.
internal sealed class ActionLedger(IUnitOfWork<ActionEntity> actions) : IActionLedger
{
    public async Task RecordAsync(ActionMessage message, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            await actions.AddAsync(
                new ActionEntity
                {
                    PartitionKey = message.Type.ToString(),
                    RowKey = message.Id,
                    Payload = message.Payload,
                    TraceParent = message.TraceParent,
                    LastSentAt = now,
                },
                cancellationToken
            );
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // A repeated request: the action is already recorded, and its payload is the same.
        }
    }

    public async Task<bool> IsPendingAsync(ActionType type, string id, CancellationToken cancellationToken) =>
        await actions.SingleOrNullAsync(type.ToString(), id, cancellationToken) is not null;

    public async Task MarkSentAsync(ActionType type, string id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await actions.UpdateAsync(
                new TableEntity(type.ToString(), id) { [nameof(ActionEntity.LastSentAt)] = now },
                ETag.All,
                TableUpdateMode.Merge,
                cancellationToken
            );
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // A fast handler already finished the action.
        }
    }

    public async Task<IReadOnlyList<ActionMessage>> UnsentSinceAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        // The ledger only ever holds unfinished actions, so a scan across types stays small.
        var rows = await actions.QueryAsync(TableClient.CreateQueryFilter($"LastSentAt le {cutoff}"), cancellationToken);

        return [.. rows.Select(row => new ActionMessage(row.RowKey, Enum.Parse<ActionType>(row.PartitionKey), row.Payload, row.TraceParent))];
    }

    public Task CompleteAsync(ActionType type, string id, CancellationToken cancellationToken) =>
        actions.DeleteAsync(type.ToString(), id, cancellationToken);
}
