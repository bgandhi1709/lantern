using Azure;
using Azure.Data.Tables;

namespace Lantern.Repository.UnitOfWork;

// createTable is for Azurite only; in Azure the Bicep owns the tables.
internal sealed class UnitOfWork<TEntity>(TableClient table, bool createTable) : IUnitOfWork<TEntity>
    where TEntity : class, ITableEntity, new()
{
    public async Task<TEntity> SingleOrNullAsync(string partitionKey, string rowKey, CancellationToken cancellationToken)
    {
        // A query, not GetEntityIfExists: that reads a missing table as "no row", and a missing table must stop the
        // request rather than read as, say, "not registered".
        var found = await QueryAsync(
            TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey} and RowKey eq {rowKey}"),
            cancellationToken
        );

        return found.Count == 0 ? null : found[0];
    }

    public Task<IReadOnlyList<TEntity>> PartitionAsync(
        string partitionKey,
        string rowKeyPrefix,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(rowKeyPrefix);

        var upperBound = rowKeyPrefix[..^1] + (char)(rowKeyPrefix[^1] + 1);

        return QueryAsync(
            TableClient.CreateQueryFilter(
                $"PartitionKey eq {partitionKey} and RowKey ge {rowKeyPrefix} and RowKey lt {upperBound}"
            ),
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<TEntity>> QueryAsync(string filter, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);

        var found = new List<TEntity>();
        await foreach (var entity in table.QueryAsync<TEntity>(filter, cancellationToken: cancellationToken))
        {
            found.Add(entity);
        }

        return found;
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);
        await table.AddEntityAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(ITableEntity entity, ETag ifMatch, TableUpdateMode mode, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);
        await table.UpdateEntityAsync(entity, ifMatch, mode, cancellationToken);
    }

    public async Task DeleteAsync(string partitionKey, string rowKey, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);
        await table.DeleteEntityAsync(partitionKey, rowKey, ETag.All, cancellationToken);
    }

    public async Task SubmitAsync(IEnumerable<TableTransactionAction> actions, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);
        await table.SubmitTransactionAsync(actions, cancellationToken);
    }

    // Idempotent every time: no flag to race on.
    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (createTable)
        {
            await table.CreateIfNotExistsAsync(cancellationToken);
        }
    }
}
