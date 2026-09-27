using System.Globalization;
using Azure;
using Azure.Data.Tables;
using Lantern.Api.Models;

namespace Lantern.Api.Repository;

// One partition per mother, so the profile and every child commit in one atomic batch.
// createTable is for Azurite only: in Azure the Bicep owns the table, and the API identity may touch entities but not create tables.
internal sealed class TableParentRepository(TableClient table, bool createTable) : IParentRepository
{
    internal const string ProfileRowKey = "profile";
    internal const string ChildRowPrefix = "child_";

    private volatile bool tableEnsured;

    public async Task<bool> TryRegisterAsync(
        ParentProfile profile,
        IReadOnlyList<ChildRecord> children,
        CancellationToken cancellationToken
    )
    {
        await this.EnsureTableAsync(cancellationToken);

        var actions = new List<TableTransactionAction>(children.Count + 1)
        {
            new(TableTransactionActionType.Add, ToEntity(profile)),
        };
        actions.AddRange(
            children.Select(child =>
                new TableTransactionAction(
                    TableTransactionActionType.Add,
                    ToEntity(profile.PartitionKey, child)
                )
            )
        );

        try
        {
            await table.SubmitTransactionAsync(actions, cancellationToken);
            return true;
        }
        catch (TableTransactionFailedException ex) when (ex.Status == 409)
        {
            return false;
        }
    }

    public async Task<ParentAggregate?> GetAsync(
        string partitionKey,
        CancellationToken cancellationToken
    )
    {
        await this.EnsureTableAsync(cancellationToken);

        ParentProfile? profile = null;
        var children = new List<ChildRecord>();

        await foreach (
            var entity in table.QueryAsync<TableEntity>(
                row => row.PartitionKey == partitionKey,
                cancellationToken: cancellationToken
            )
        )
        {
            if (entity.RowKey == ProfileRowKey)
            {
                profile = ToProfile(entity);
            }
            else if (entity.RowKey.StartsWith(ChildRowPrefix, StringComparison.Ordinal))
            {
                children.Add(ToChild(entity));
            }
        }

        return profile is null
            ? null
            : new ParentAggregate(profile, [.. children.OrderBy(child => child.Position)]);
    }

    internal static string ChildRowKey(Guid childId) =>
        ChildRowPrefix + childId.ToString("N", CultureInfo.InvariantCulture);

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (!createTable || this.tableEnsured)
        {
            return;
        }

        await table.CreateIfNotExistsAsync(cancellationToken);
        this.tableEnsured = true;
    }

    private static TableEntity ToEntity(ParentProfile profile) =>
        new(profile.PartitionKey, ProfileRowKey)
        {
            ["FamilyId"] = profile.FamilyId.ToString("D"),
            ["NameCipher"] = profile.NameCipher,
            ["EmailCipher"] = profile.EmailCipher,
            ["Region"] = profile.Region,
            ["Language"] = profile.Language,
            ["ConsentVersion"] = profile.ConsentVersion,
            ["ConsentAt"] = profile.ConsentAt,
            ["CreatedAt"] = profile.CreatedAt,
        };

    private static TableEntity ToEntity(string partitionKey, ChildRecord child)
    {
        var entity = new TableEntity(partitionKey, ChildRowKey(child.ChildId))
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

    private static ParentProfile ToProfile(TableEntity entity) =>
        new(
            entity.PartitionKey,
            Guid.Parse(entity.GetString("FamilyId")),
            entity.GetString("NameCipher"),
            entity.GetString("EmailCipher"),
            entity.GetString("Region"),
            entity.GetString("Language"),
            entity.GetString("ConsentVersion"),
            entity.GetDateTimeOffset("ConsentAt")!.Value,
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
