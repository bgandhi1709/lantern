using Lantern.Core.Models;

namespace Lantern.Core.Repository;

public interface IFamilyRepository : IRepositoryBase<Family>
{
    Task<Parent?> FindParentAsync(string uid, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the Family, its Children and the Parent, creating the Family key. The Parent's profile row is written last
    /// and is the commit point.
    /// </summary>
    /// <exception cref="Exceptions.AlreadyRegisteredException">The uid already has a profile.</exception>
    Task RegisterAsync(string uid, Family family, Parent parent, IReadOnlyList<Child> children, CancellationToken cancellationToken);
}
