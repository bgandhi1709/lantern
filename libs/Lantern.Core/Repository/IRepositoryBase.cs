using Lantern.Core.Models;

namespace Lantern.Core.Repository;

/// <summary>Stores one model type inside a Family. Every call names the Family, so nothing can read across Families.</summary>
public interface IRepositoryBase<T>
    where T : class, IFamilyModel
{
    /// <exception cref="Exceptions.NotFoundException">No such row in this Family.</exception>
    Task<T> SingleAsync(Guid familyId, Guid id, CancellationToken cancellationToken);

    Task<T?> SingleOrNullAsync(Guid familyId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<T>> CollectionAsync(Guid familyId, CancellationToken cancellationToken);

    Task<T> AddAsync(T instance, CancellationToken cancellationToken);

    Task<T> UpdateAsync(T instance, CancellationToken cancellationToken);

    /// <summary>Safe to repeat: a row that is already gone is not an error.</summary>
    Task RemoveAsync(Guid familyId, Guid id, CancellationToken cancellationToken);
}
