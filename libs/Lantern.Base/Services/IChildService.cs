using Lantern.Core.Models;
using Lantern.Core.Service;

namespace Lantern.Base.Services;

/// <remarks>
/// <see cref="IServiceBase{T}.UpdateAsync"/> changes the locked Name, School and BirthYear, never the Class.
/// <see cref="IServiceBase{T}.RemoveAsync"/> records the delete and returns; <c>Lantern.Functions</c> finishes it (ADR-0003).
/// </remarks>
public interface IChildService : IServiceBase<Child>
{
    /// <summary>Adds the Child, or returns the stored one unchanged when its id is already in the Family.</summary>
    /// <exception cref="Core.Exceptions.LanternException">(<c>ChildLimitReached</c>) The Family already has six Children.</exception>
    /// <exception cref="Core.Exceptions.LanternException">(<c>ChildDeleting</c>) That id is being deleted.</exception>
    /// <exception cref="Core.Exceptions.LanternException">(<c>ClassNotAvailable</c>) The Class has no Books for the Family's Board.</exception>
    Task<(Child Child, bool Created)> AddOrGetAsync(Child child, CancellationToken cancellationToken);
}
