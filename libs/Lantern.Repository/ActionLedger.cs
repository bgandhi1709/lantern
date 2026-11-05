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

    private const int MaxParallelDeletes = 8;

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
        catch (RequestFailedException ex) when (IsAlreadyThere(ex))
        {
            // A repeated request: the action is already recorded, and its payload is the same.
        }
    }

    public async Task<bool> IsPendingAsync(ActionType type, string id, string subscriber, CancellationToken cancellationToken) =>
        await actions.SingleOrNullAsync(type.ToString(), id, cancellationToken) is { } row && !FinishedSubscribers(row).Contains(subscriber);

    public async Task MarkSentAsync(ActionType type, string id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await actions.UpdateAsync(
                new TableEntity(type.ToString(), id) { [nameof(ActionEntity.ConfirmedAt)] = now, [nameof(ActionEntity.LastSentAt)] = now },
                ETag.All,
                TableUpdateMode.Merge,
                cancellationToken
            );
        }
        catch (RequestFailedException ex) when (IsMissing(ex)) { }
    }

    public async Task<IReadOnlyList<ActionMessage>> ResendableAsync(
        DateTimeOffset unconfirmedBefore,
        DateTimeOffset confirmedBefore,
        CancellationToken cancellationToken
    )
    {
        var newest = unconfirmedBefore > confirmedBefore ? unconfirmedBefore : confirmedBefore;
        var rows = await actions.QueryAsync(TableClient.CreateQueryFilter($"LastSentAt le {newest}"), cancellationToken);

        return
        [
            .. rows
                .Where(row => FinishedSubscribers(row).Length == 0 && row.LastSentAt <= (row.ConfirmedAt is null ? unconfirmedBefore : confirmedBefore))
                .Select(row => new ActionMessage(row.RowKey, Enum.Parse<ActionType>(row.PartitionKey), row.Payload, row.TraceParent)),
        ];
    }

    public async Task MarkDoneAsync(ActionType type, string id, string subscriber, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxMarkAttempts; attempt++)
        {
            if (await actions.SingleOrNullAsync(type.ToString(), id, cancellationToken) is not { } row || FinishedSubscribers(row).Contains(subscriber))
            {
                return;
            }

            try
            {
                await actions.UpdateAsync(
                    new TableEntity(type.ToString(), id) { [nameof(ActionEntity.DoneBy)] = string.Join(Separator, FinishedSubscribers(row).Append(subscriber)) },
                    row.ETag,
                    TableUpdateMode.Merge,
                    cancellationToken
                );

                return;
            }
            catch (RequestFailedException ex) when (IsMissing(ex))
            {
                return;
            }
            catch (RequestFailedException ex) when (IsLostRace(ex)) { }
        }

        throw new InvalidOperationException($"The done marker of {subscriber} on {type} {id} lost the race {MaxMarkAttempts} times.");
    }

    public async Task ExpireDoneAsync(DateTimeOffset recordedBefore, CancellationToken cancellationToken)
    {
        var rows = await actions.QueryAsync(TableClient.CreateQueryFilter($"RecordedAt le {recordedBefore} and DoneBy ne ''"), cancellationToken);

        await Parallel.ForEachAsync(
            rows,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDeletes, CancellationToken = cancellationToken },
            async (row, token) =>
            {
                try
                {
                    await actions.DeleteAsync(row.PartitionKey, row.RowKey, token);
                }
                catch (RequestFailedException ex) when (IsMissing(ex)) { }
            }
        );
    }

    private static bool IsMissing(RequestFailedException ex) => ex.Status == 404;

    private static bool IsAlreadyThere(RequestFailedException ex) => ex.Status == 409;

    private static bool IsLostRace(RequestFailedException ex) => ex.Status == 412;

    private static string[] FinishedSubscribers(ActionEntity row) => row.DoneBy.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
}
