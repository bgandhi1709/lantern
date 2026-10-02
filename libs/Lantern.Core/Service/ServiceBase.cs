using Lantern.Core.Exceptions;
using Lantern.Core.Identity;
using Lantern.Core.Models;
using Lantern.Core.Repository;

namespace Lantern.Core.Service;

// The Family comes from the caller's token and overwrites whatever the instance carried, so a request can never name
// another Family; an id from another Family reads as unknown.
public class ServiceBase<T, TRepository>(TRepository repository, IIdentityResolver identityResolver, IFamilyRepository families)
    : IServiceBase<T>
    where T : class, IFamilyModel
    where TRepository : IRepositoryBase<T>
{
    private Guid? familyId;

    protected TRepository Repository => repository;

    protected IFamilyRepository Families => families;

    protected CallerIdentity Identity => identityResolver.Identity;

    public virtual async Task<T> SingleAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.SingleAsync(await FamilyIdAsync(cancellationToken), id, cancellationToken);

    public virtual async Task<IReadOnlyList<T>> CollectionAsync(CancellationToken cancellationToken) =>
        await repository.CollectionAsync(await FamilyIdAsync(cancellationToken), cancellationToken);

    public virtual async Task<T> AddAsync(T instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);

        instance.FamilyId = await FamilyIdAsync(cancellationToken);

        return await repository.AddAsync(instance, cancellationToken);
    }

    public virtual async Task<T> UpdateAsync(T instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);

        instance.FamilyId = await FamilyIdAsync(cancellationToken);

        return await repository.UpdateAsync(instance, cancellationToken);
    }

    public virtual async Task RemoveAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.RemoveAsync(await FamilyIdAsync(cancellationToken), id, cancellationToken);

    /// <exception cref="NotRegisteredException">The caller has no profile.</exception>
    protected async Task<Guid> FamilyIdAsync(CancellationToken cancellationToken) =>
        familyId ??= (
            await families.FindParentAsync(Identity.Uid, cancellationToken) ?? throw new NotRegisteredException()
        ).FamilyId;
}

/// <summary>The service any model gets without code of its own.</summary>
public class ServiceBase<T>(IRepositoryBase<T> repository, IIdentityResolver identityResolver, IFamilyRepository families)
    : ServiceBase<T, IRepositoryBase<T>>(repository, identityResolver, families)
    where T : class, IFamilyModel;
