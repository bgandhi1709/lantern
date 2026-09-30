using Lantern.Api.Models;

namespace Lantern.Api.Repository;

public interface IFamilyRepository
{
    Task<bool> TryRegisterAsync(
        ParentProfile parent,
        FamilyRecord family,
        IReadOnlyList<ChildRecord> children,
        CancellationToken cancellationToken
    );

    Task<FamilyAggregate?> GetAsync(string partitionKey, CancellationToken cancellationToken);
}
