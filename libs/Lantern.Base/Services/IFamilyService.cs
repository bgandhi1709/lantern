using Lantern.Core.Models;
using Lantern.Core.Service;

namespace Lantern.Base.Services;

public interface IFamilyService : IServiceBase<Family>
{
    /// <summary>Registers the caller as the first Parent of a new Family with its Children.</summary>
    /// <exception cref="Core.Exceptions.AlreadyRegisteredException">The caller already has a Family.</exception>
    /// <exception cref="Core.Exceptions.InvalidRequestException">A rule the API model can't express is broken.</exception>
    Task<Family> RegisterAsync(Registration registration, CancellationToken cancellationToken);

    /// <summary>The caller's Family, with the caller's own profile and the Children that are not being deleted.</summary>
    /// <exception cref="Core.Exceptions.NotRegisteredException">The caller has no Family.</exception>
    Task<Family> MeAsync(CancellationToken cancellationToken);
}
