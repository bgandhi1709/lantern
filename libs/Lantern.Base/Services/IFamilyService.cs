using Lantern.Core.Models;
using Lantern.Core.Service;

namespace Lantern.Base.Services;

public interface IFamilyService : IServiceBase<Family>
{
    Task<Family> RegisterAsync(Registration registration, CancellationToken cancellationToken);

    Task<Family> MeAsync(CancellationToken cancellationToken);

    Task EraseAsync(string proof, CancellationToken cancellationToken);
}
