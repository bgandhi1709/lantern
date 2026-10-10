using Lantern.Core.Models;

namespace Lantern.Core.Repository;

public interface IFamilyRepository : IRepositoryBase<Family>
{
    Task<Parent?> FindParentAsync(string uid, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the Family (with the keys the phone wrapped), its Children and the Parent. The Parent's profile row is written last
    /// and is the commit point.
    /// </summary>
    /// <exception cref="Exceptions.AlreadyRegisteredException">The uid already has a profile.</exception>
    /// <exception cref="Exceptions.FamilyIdTakenException">A Family with this id already exists.</exception>
    Task RegisterAsync(string uid, Family family, Parent parent, IReadOnlyList<Child> children, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the profile of every Parent of the Family, so none of them is registered any more. A profile that already
    /// belongs to a new Family is kept. Safe to repeat.
    /// </summary>
    Task RemoveParentsAsync(Guid familyId, CancellationToken cancellationToken);

    /// <summary>Removes the Parents' profiles, clears the wrapped Family keys and deletes every row of the Family. Safe to repeat.</summary>
    Task EraseAsync(Guid familyId, CancellationToken cancellationToken);
}
