using Azure;
using Azure.Data.Tables;

namespace Lantern.Repository.UnitOfWork;

/// <summary>The only way into a table. Entities of different types can share a table, and so a transaction.</summary>
internal interface IUnitOfWork<TEntity>
    where TEntity : class, ITableEntity, new()
{
    Task<TEntity> SingleOrNullAsync(string partitionKey, string rowKey, CancellationToken cancellationToken);

    /// <summary>The rows of the partition whose row key starts with the prefix.</summary>
    Task<IReadOnlyList<TEntity>> PartitionAsync(string partitionKey, string rowKeyPrefix, CancellationToken cancellationToken);

    Task<IReadOnlyList<TEntity>> QueryAsync(string filter, CancellationToken cancellationToken);

    /// <exception cref="RequestFailedException">409 if the row exists.</exception>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken);

    /// <exception cref="RequestFailedException">412 if the row changed since <paramref name="ifMatch"/>, 404 if it is gone.</exception>
    Task UpdateAsync(ITableEntity entity, ETag ifMatch, TableUpdateMode mode, CancellationToken cancellationToken);

    /// <summary>Safe to repeat: a row that is already gone is not an error.</summary>
    Task DeleteAsync(string partitionKey, string rowKey, CancellationToken cancellationToken);

    /// <exception cref="TableTransactionFailedException">Any action failed; none was applied.</exception>
    Task SubmitAsync(IEnumerable<TableTransactionAction> actions, CancellationToken cancellationToken);
}
