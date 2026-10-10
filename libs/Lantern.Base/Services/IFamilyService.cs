using Lantern.Core.Models;
using Lantern.Core.Service;

namespace Lantern.Base.Services;

public interface IFamilyService : IServiceBase<Family>
{
    /// <summary>Registers the caller as the first Parent of a new Family with its Children.</summary>
    /// <exception cref="Core.Exceptions.LanternException">(<c>AlreadyRegistered</c>) The caller already has a Family.</exception>
    /// <exception cref="Core.Exceptions.LanternException">(<c>FamilyIdTaken</c>) A Family with the phone's chosen id already exists.</exception>
    /// <exception cref="Core.Exceptions.LanternException">(<c>ClassNotAvailable</c>) A Child's Class has no Books for the Board.</exception>
    /// <exception cref="Core.Exceptions.LanternException">(<c>InvalidRequest</c>) A rule the API model can't express is broken.</exception>
    Task<Family> RegisterAsync(Registration registration, CancellationToken cancellationToken);

    /// <summary>The caller's Family, with the caller's own profile and the Children that are not being deleted.</summary>
    /// <exception cref="Core.Exceptions.LanternException">(<c>NotRegistered</c>) The caller has no Family.</exception>
    Task<Family> MeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the caller's Family: its Parents are no longer registered when this returns, and the rest of its data is
    /// erased in Lantern.Functions.
    /// </summary>
    /// <exception cref="Core.Exceptions.LanternException">(<c>NotRegistered</c>) The caller has no Family.</exception>
    Task EraseAsync(CancellationToken cancellationToken);
}
