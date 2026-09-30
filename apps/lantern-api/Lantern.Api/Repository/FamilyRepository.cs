using System.Globalization;
using Azure;
using Azure.Data.Tables;
using Lantern.Api.Models;

namespace Lantern.Api.Repository;

// Table batches never span partitions, so a registration is two writes: the family batch, then the parent's
// profile row as the commit point. createTables is for Azurite only; in Azure the Bicep owns the tables.
internal sealed class FamilyRepository(TableClient parents, TableClient families, bool createTables)
    : IFamilyRepository
{
    internal const string ProfileRowKey = "profile";
    internal const string FamilyRowKey = "family";
    private const string ChildRowPrefix = "child_";
    private const string ParentRowPrefix = "parent_";

    private volatile bool _tablesEnsured;

    public async Task<bool> TryRegisterAsync(
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
            return false;
        }

        var familyPartition = FamilyPartition(family.FamilyId);
        var actions = new List<TableTransactionAction>(children.Count + 2)
        {
            new(TableTransactionActionType.Add, ToEntity(familyPartition, family)),
            new(TableTransactionActionType.Add, ToMembership(familyPartition, parent)),
        };
        actions.AddRange(
            children.Select(child =>
                new TableTransactionAction(TableTransactionActionType.Add, ToEntity(familyPartition, child))
            )
        );

        await families.SubmitTransactionAsync(actions, cancellationToken);

        try
        {
            await parents.AddEntityAsync(ToEntity(parent), cancellationToken);
            return true;
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

            return false;
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

        var parent = ToParent(profile);
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
                family = ToFamily(entity);
            }
            else if (entity.RowKey.StartsWith(ChildRowPrefix, StringComparison.Ordinal))
            {
                children.Add(ToChild(entity));
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

    private static TableEntity ToEntity(ParentProfile parent) =>
        new(parent.PartitionKey, ProfileRowKey)
        {
            ["ParentId"] = parent.ParentId.ToString("D"),
            ["FamilyId"] = parent.FamilyId.ToString("D"),
            ["NameCipher"] = parent.NameCipher,
            ["EmailCipher"] = parent.EmailCipher,
            ["Language"] = parent.Language,
            ["ConsentVersion"] = parent.ConsentVersion,
            ["ConsentAt"] = parent.ConsentAt,
            ["CreatedAt"] = parent.CreatedAt,
        };

    private static TableEntity ToEntity(string familyPartition, FamilyRecord family) =>
        new(familyPartition, FamilyRowKey)
        {
            ["Region"] = family.Region,
            ["WrappedFieldKey"] = family.WrappedFieldKey,
            ["KeyScheme"] = family.KeyScheme,
            ["CreatedAt"] = family.CreatedAt,
        };

    private static TableEntity ToMembership(string familyPartition, ParentProfile parent) =>
        new(familyPartition, ParentRowPrefix + parent.PartitionKey)
        {
            ["ParentId"] = parent.ParentId.ToString("D"),
            ["CreatedAt"] = parent.CreatedAt,
        };

    private static TableEntity ToEntity(string familyPartition, ChildRecord child)
    {
        var entity = new TableEntity(familyPartition, ChildRowKey(child.ChildId))
        {
            ["NameCipher"] = child.NameCipher,
            ["ClassLevel"] = child.ClassLevel,
            ["BirthYear"] = child.BirthYear,
            ["Position"] = child.Position,
            ["CreatedAt"] = child.CreatedAt,
        };

        if (child.SchoolCipher is not null)
        {
            entity["SchoolCipher"] = child.SchoolCipher;
        }

        return entity;
    }

    private static ParentProfile ToParent(TableEntity entity) =>
        new(
            entity.PartitionKey,
            Guid.Parse(entity.GetString("ParentId")),
            Guid.Parse(entity.GetString("FamilyId")),
            entity.GetString("NameCipher"),
            entity.GetString("EmailCipher"),
            entity.GetString("Language"),
            entity.GetString("ConsentVersion"),
            entity.GetDateTimeOffset("ConsentAt")!.Value,
            entity.GetDateTimeOffset("CreatedAt")!.Value
        );

    private static FamilyRecord ToFamily(TableEntity entity) =>
        new(
            Guid.Parse(entity.PartitionKey),
            entity.GetString("Region"),
            entity.GetString("WrappedFieldKey"),
            entity.GetString("KeyScheme"),
            entity.GetDateTimeOffset("CreatedAt")!.Value
        );

    private static ChildRecord ToChild(TableEntity entity) =>
        new(
            Guid.ParseExact(entity.RowKey[ChildRowPrefix.Length..], "N"),
            entity.GetString("NameCipher"),
            entity.GetString("SchoolCipher"),
            entity.GetInt32("ClassLevel")!.Value,
            entity.GetInt32("BirthYear")!.Value,
            entity.GetInt32("Position")!.Value,
            entity.GetDateTimeOffset("CreatedAt")!.Value
        );
}
