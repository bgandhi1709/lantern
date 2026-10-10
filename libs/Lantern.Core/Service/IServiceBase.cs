using Lantern.Core.Models;

namespace Lantern.Core.Service;

/// <summary>A model's operations for the caller, always inside the caller's own Family.</summary>
public interface IServiceBase<T>
    where T : class, IFamilyModel
{
    /// <exception cref="Exceptions.LanternException">No such id in the caller's Family (a <c>NotFound</c> code).</exception>
    Task<T> SingleAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<T>> CollectionAsync(CancellationToken cancellationToken);

    Task<T> AddAsync(T instance, CancellationToken cancellationToken);

    Task<T> UpdateAsync(T instance, CancellationToken cancellationToken);

    Task RemoveAsync(Guid id, CancellationToken cancellationToken);
}
