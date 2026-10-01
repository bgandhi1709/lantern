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

    Task<FamilyAggregate?> GetAsync(string partitionKey, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.FamilyChangedException">The Family changed since it was read, or the child id is taken.</exception>
    Task AddChildAsync(Guid familyId, ChildRecord child, string familyETag, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.ChildNotFoundException">The child is gone or being deleted.</exception>
    Task UpdateChildAsync(
        Guid familyId,
        Guid childId,
        string nameCipher,
        string? schoolCipher,
        int birthYear,
        CancellationToken cancellationToken
    );
}
