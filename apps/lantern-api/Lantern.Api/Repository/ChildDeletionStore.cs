using System.Globalization;
using Azure;
using Azure.Data.Tables;
using Lantern.Api.Exceptions;
using Lantern.Api.Models;

namespace Lantern.Api.Repository;

// Pending jobs live in their own partition of the families table. The pending row is the commit point of a delete:
// once it exists the worker finishes the job, whatever happened to the request that wrote it.
internal sealed class ChildDeletionStore(TableClient families, IRowKeys keys, bool createTables) : IChildDeletionStore
{
    internal const string PendingPartition = "deletes";
    private const int MaxLeaseMinutes = 10;

    public async Task RequestAsync(Guid familyId, Guid childId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);

        var familyPartition = keys.FamilyPartition(familyId);
        var childRow = keys.ChildRowKey(childId);
        var existing = await families.GetEntityIfExistsAsync<TableEntity>(
            familyPartition,
            childRow,
            select: [],
            cancellationToken: cancellationToken
        );
        if (!existing.HasValue)
        {
            throw new ChildNotFoundException();
        }

        var pending = new TableEntity(PendingPartition, PendingRowKey(familyId, childId))
        {
            ["FamilyId"] = familyId,
            ["ChildId"] = childId,
            ["Attempts"] = 0,
            ["LeaseUntil"] = now,
        };
        try
        {
            await families.AddEntityAsync(pending, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // A repeated request: the job is already recorded.
        }

        try
        {
            await families.UpdateEntityAsync(
                new TableEntity(familyPartition, childRow) { ["Status"] = nameof(ChildStatus.Deleting) },
                ETag.All,
                TableUpdateMode.Merge,
                cancellationToken
            );
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // The worker already finished it.
        }
    }

    public async Task<PendingDelete?> ClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);

        await foreach (
            var row in families.QueryAsync<TableEntity>(
                TableClient.CreateQueryFilter($"PartitionKey eq {PendingPartition} and LeaseUntil le {now}"),
                maxPerPage: 10,
                cancellationToken: cancellationToken
            )
        )
        {
            var attempts = row.GetInt32("Attempts") ?? 0;
            // A job that keeps failing backs off, up to ten minutes.
            var lease = TimeSpan.FromMinutes(Math.Min(attempts + 1, MaxLeaseMinutes));
            row["Attempts"] = attempts + 1;
            row["LeaseUntil"] = now + lease;

            try
            {
                await families.UpdateEntityAsync(row, row.ETag, TableUpdateMode.Merge, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status is 412 or 404)
            {
                // Another replica took it, or it just finished.
                continue;
            }

            return new PendingDelete(row.GetGuid("FamilyId")!.Value, row.GetGuid("ChildId")!.Value, attempts + 1);
        }

        return null;
    }

    public async Task RemoveChildRowAsync(PendingDelete job, CancellationToken cancellationToken)
    {
        await families.DeleteEntityAsync(
            keys.FamilyPartition(job.FamilyId),
            keys.ChildRowKey(job.ChildId),
            cancellationToken: cancellationToken
        );
    }

    public async Task RemovePendingAsync(PendingDelete job, CancellationToken cancellationToken)
    {
        await families.DeleteEntityAsync(
            PendingPartition,
            PendingRowKey(job.FamilyId, job.ChildId),
            cancellationToken: cancellationToken
        );
    }

    private static string PendingRowKey(Guid familyId, Guid childId) =>
        string.Create(CultureInfo.InvariantCulture, $"{familyId:N}_{childId:N}");

    // Idempotent every time: no flag to race on.
    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (createTables)
        {
            await families.CreateIfNotExistsAsync(cancellationToken);
        }
    }
}
