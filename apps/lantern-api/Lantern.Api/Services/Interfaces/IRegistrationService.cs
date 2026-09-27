using Lantern.Api.Auth;
using Lantern.Api.Contracts;
using Lantern.Api.Models;

namespace Lantern.Api.Services.Interfaces;

public interface IRegistrationService
{
    Task<FamilyView> RegisterAsync(Caller caller, RegisterBody body, CancellationToken cancellationToken);

    Task<FamilyView> GetAsync(Caller caller, CancellationToken cancellationToken);
}
