using Lantern.Api.Models;

namespace Lantern.Api.Repository;

public interface IParentRepository
{
    Task<bool> TryRegisterAsync(
        ParentProfile profile,
        IReadOnlyList<ChildRecord> children,
        CancellationToken cancellationToken
    );

    Task<ParentAggregate?> GetAsync(string partitionKey, CancellationToken cancellationToken);
}
