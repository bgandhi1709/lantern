using System.Globalization;
using Azure;
using Azure.Data.Tables;
using Lantern.Api.Exceptions;
using Lantern.Api.Models;
using Mapster;

namespace Lantern.Api.Repository;

// Table batches never span partitions, so a registration is two writes: the family batch, then the parent's
// profile row as the commit point. createTables is for Azurite only; in Azure the Bicep owns the tables.
internal sealed class FamilyRepository(TableClient parents, TableClient families, bool createTables)
    : IFamilyRepository
{
    internal const string ProfileRowKey = "profile";
    private const string FamilyRowKey = "family";
    private const string ChildRowPrefix = "child_";
    private const string ParentRowPrefix = "parent_";

    private volatile bool _tablesEnsured;

    public async Task RegisterAsync(
        ParentProfile parent,
        FamilyRecord family,
        IReadOnlyList<ChildRecord> children,
        CancellationToken cancellationToken
    )
    {
        await EnsureTablesAsync(cancellationToken);

        // Lookup first so a repeat register writes nothing; the profile Add below still settles a real race.
        var existing = await parents.GetEntityIfExistsAsync<TableEntity>(
            parent.PartitionKey,
            ProfileRowKey,
            select: [],
            cancellationToken: cancellationToken
        );
        if (existing.HasValue)
        {
            throw new AlreadyRegisteredException();
        }

        var familyPartition = FamilyPartition(family.FamilyId);
        var membership = new TableEntity(familyPartition, ParentRowPrefix + parent.PartitionKey)
        {
            ["ParentId"] = parent.ParentId,
            ["CreatedAt"] = parent.CreatedAt,
        };
        List<TableTransactionAction> actions =
        [
            new(TableTransactionActionType.Add, ToEntity(family, familyPartition, FamilyRowKey)),
            new(TableTransactionActionType.Add, membership),
            .. children.Select(child =>
                new TableTransactionAction(
                    TableTransactionActionType.Add,
                    ToEntity(child, familyPartition, ChildRowKey(child.ChildId))
                )
            ),
        ];

        await families.SubmitTransactionAsync(actions, cancellationToken);

        try
        {
            await parents.AddEntityAsync(ToEntity(parent, parent.PartitionKey, ProfileRowKey), cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Lost the race: this attempt's family rows are unreachable, so take them back.
            await families.SubmitTransactionAsync(
                actions.Select(action => new TableTransactionAction(
                    TableTransactionActionType.Delete,
                    new TableEntity(action.Entity.PartitionKey, action.Entity.RowKey)
                )),
                cancellationToken
            );

            throw new AlreadyRegisteredException();
        }
    }

    public async Task<FamilyAggregate?> GetAsync(string partitionKey, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);

        // A query, not GetEntityIfExists: that treats a missing table as "no row", and a missing table
        // must stop the request rather than read as "not registered".
        var profile = await parents
            .QueryAsync<TableEntity>(
                row => row.PartitionKey == partitionKey && row.RowKey == ProfileRowKey,
                cancellationToken: cancellationToken
            )
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var parent = profile.Adapt<ParentProfile>();
        var familyPartition = FamilyPartition(parent.FamilyId);
        FamilyRecord? family = null;
        var children = new List<ChildRecord>();

        await foreach (
            var entity in families.QueryAsync<TableEntity>(
                row => row.PartitionKey == familyPartition,
                cancellationToken: cancellationToken
            )
        )
        {
            if (entity.RowKey == FamilyRowKey)
            {
                family = entity.Adapt<FamilyRecord>();
            }
            else if (entity.RowKey.StartsWith(ChildRowPrefix, StringComparison.Ordinal))
            {
                children.Add(entity.Adapt<ChildRecord>());
            }
        }

        return family is null
            ? throw new InvalidOperationException("A parent's profile points at a family that has no family row.")
            : new FamilyAggregate(parent, family, [.. children.OrderBy(child => child.Position)]);
    }

    internal static string FamilyPartition(Guid familyId) => familyId.ToString("D", CultureInfo.InvariantCulture);

    internal static string ChildRowKey(Guid childId) =>
        ChildRowPrefix + childId.ToString("N", CultureInfo.InvariantCulture);

    private async Task EnsureTablesAsync(CancellationToken cancellationToken)
    {
        if (!createTables || _tablesEnsured)
        {
            return;
        }

        await parents.CreateIfNotExistsAsync(cancellationToken);
        await families.CreateIfNotExistsAsync(cancellationToken);
        _tablesEnsured = true;
    }

    private static TableEntity ToEntity<T>(T model, string partitionKey, string rowKey)
        where T : notnull
    {
        var entity = new TableEntity(partitionKey, rowKey);
        foreach (var (name, value) in model.Adapt<Dictionary<string, object?>>())
        {
            if (value is not null)
            {
                entity[name] = value is Enum ? value.ToString() : value;
            }
        }

        return entity;
    }
}
