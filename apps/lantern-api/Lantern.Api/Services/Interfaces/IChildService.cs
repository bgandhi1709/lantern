using Lantern.Api.Auth;
using Lantern.Api.Contracts;
using Lantern.Api.Models;

namespace Lantern.Api.Services.Interfaces;

public sealed record AddChildResult(ChildView Child, bool Created);

public interface IChildService
{
    Task<AddChildResult> AddAsync(Caller caller, AddChildBody body, CancellationToken cancellationToken);

    Task<ChildView> EditAsync(Caller caller, Guid childId, ChildDetailsBody body, CancellationToken cancellationToken);

    Task DeleteAsync(Caller caller, Guid childId, CancellationToken cancellationToken);
}
