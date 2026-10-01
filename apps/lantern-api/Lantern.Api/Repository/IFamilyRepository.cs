using Lantern.Api.Models;

namespace Lantern.Api.Repository;

public interface IFamilyRepository
{
    /// <exception cref="Exceptions.AlreadyRegisteredException">The caller already has a profile.</exception>
    Task RegisterAsync(
        ParentProfile parent,
        FamilyRecord family,
        IReadOnlyList<ChildRecord> children,
        CancellationToken cancellationToken
    );

    /// <exception cref="Exceptions.AlreadyRegisteredException">The Parent already has a profile.</exception>
    Task JoinAsync(ParentProfile parent, CancellationToken cancellationToken);

    Task<FamilyAggregate?> GetAsync(string partitionKey, CancellationToken cancellationToken);
}
