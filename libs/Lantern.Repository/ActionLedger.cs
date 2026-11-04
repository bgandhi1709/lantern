using Azure;
using Azure.Data.Tables;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Lantern.Repository.Entities;
using Lantern.Repository.UnitOfWork;

namespace Lantern.Repository;

// Partition is the action type and the row key the action id, so a repeat of the same request is the same row.
internal sealed class ActionLedger(IUnitOfWork<ActionEntity> actions) : IActionLedger
{
    private const char Separator = ',';

    private const int MaxMarkAttempts = 8;

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
                    RecordedAt = now,
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

    public async Task<bool> IsPendingAsync(ActionType type, string id, string subscriber, CancellationToken cancellationToken) =>
        await actions.SingleOrNullAsync(type.ToString(), id, cancellationToken) is { } row && !Subscribers(row).Contains(subscriber);

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
        }
    }

    public async Task<IReadOnlyList<ActionMessage>> UnsentSinceAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var rows = await actions.QueryAsync(TableClient.CreateQueryFilter($"LastSentAt le {cutoff}"), cancellationToken);

        return
        [
            .. rows
                .Where(row => Subscribers(row).Length == 0)
                .Select(row => new ActionMessage(row.RowKey, Enum.Parse<ActionType>(row.PartitionKey), row.Payload, row.TraceParent)),
        ];
    }

    public async Task MarkDoneAsync(ActionType type, string id, string subscriber, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxMarkAttempts; attempt++)
        {
            if (await actions.SingleOrNullAsync(type.ToString(), id, cancellationToken) is not { } row || Subscribers(row).Contains(subscriber))
            {
                return;
            }

            try
            {
                await actions.UpdateAsync(
                    new TableEntity(type.ToString(), id) { [nameof(ActionEntity.DoneBy)] = string.Join(Separator, Subscribers(row).Append(subscriber)) },
                    row.ETag,
                    TableUpdateMode.Merge,
                    cancellationToken
                );

                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 412) { }
        }

        throw new InvalidOperationException($"The done marker of {subscriber} on {type} {id} lost the race {MaxMarkAttempts} times.");
    }

    public async Task ExpireDoneAsync(DateTimeOffset recordedBefore, CancellationToken cancellationToken)
    {
        var rows = await actions.QueryAsync(TableClient.CreateQueryFilter($"RecordedAt le {recordedBefore} and DoneBy ne ''"), cancellationToken);

        foreach (var row in rows)
        {
            try
            {
                await actions.DeleteAsync(row.PartitionKey, row.RowKey, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 404) { }
        }
    }

    private static string[] Subscribers(ActionEntity row) => row.DoneBy.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
}
