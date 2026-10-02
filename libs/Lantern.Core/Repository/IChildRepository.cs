using Lantern.Core.Models;

namespace Lantern.Core.Repository;

/// <remarks>
/// <see cref="IRepositoryBase{T}.AddAsync"/> enforces <see cref="Child.MaxPerFamily"/> against concurrent adds and
/// sets the Position; <see cref="IRepositoryBase{T}.UpdateAsync"/> changes only Name, School and BirthYear, never a
/// Child being deleted.
/// </remarks>
public interface IChildRepository : IRepositoryBase<Child>
{
    /// <summary>Hides the Child from its Parents. Safe to repeat, and a row that is already gone is not an error.</summary>
    Task MarkDeletingAsync(Guid familyId, Guid childId, CancellationToken cancellationToken);

    /// <summary>The Child's status, or null when there is no row. Decrypts nothing, so it needs no Family key.</summary>
    Task<ChildStatus?> StatusAsync(Guid familyId, Guid childId, CancellationToken cancellationToken);
}
