using Azure;
using Azure.Data.Tables;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Repository.Security;
using Lantern.Repository.UnitOfWork;
using MapsterMapper;

namespace Lantern.Repository;

// Maps a service model to its entity and back, encrypting the entity's [Encrypted] columns with the Family key on the
// way. A model type needs only its row key; it overrides the rest only where its storage rules differ.
internal abstract class BaseRepository<TModel, TEntity>(
    IUnitOfWork<TEntity> unitOfWork,
    IFamilyKeyRing keyRing,
    IFieldProtector protector,
    IMapper mapper,
    IRowKeyService keyService
) : IRepositoryBase<TModel>
    where TModel : class, IFamilyModel
    where TEntity : class, ITableEntity, new()
{
    protected IUnitOfWork<TEntity> UnitOfWork => unitOfWork;

    protected IRowKeyService KeyService => keyService;

    protected IFamilyKeyRing KeyRing => keyRing;

    protected IFieldProtector Protector => protector;

    protected IMapper Mapper => mapper;

    /// <summary>The prefix shared by the row keys of this type, so a partition read returns only these rows.</summary>
    protected abstract string RowKeyPrefix { get; }

    protected abstract string RowKey(Guid id);

    protected abstract Guid IdOf(TModel model);

    public virtual async Task<TModel?> SingleOrNullAsync(Guid familyId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.SingleOrNullAsync(keyService.FamilyPartition(familyId), RowKey(id), cancellationToken);

        return entity is null ? null : await ToModelAsync(familyId, entity, cancellationToken);
    }

    public virtual async Task<TModel> SingleAsync(Guid familyId, Guid id, CancellationToken cancellationToken) =>
        await SingleOrNullAsync(familyId, id, cancellationToken)
        ?? throw new NotFoundException(typeof(TModel).Name.ToLowerInvariant());

    public virtual async Task<IReadOnlyList<TModel>> CollectionAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var models = new List<TModel>();
        foreach (var entity in await unitOfWork.PartitionAsync(keyService.FamilyPartition(familyId), RowKeyPrefix, cancellationToken))
        {
            models.Add(await ToModelAsync(familyId, entity, cancellationToken));
        }

        return models;
    }

    public virtual async Task<TModel> AddAsync(TModel instance, CancellationToken cancellationToken)
    {
        await unitOfWork.AddAsync(await ToEntityAsync(instance, cancellationToken), cancellationToken);

        return instance;
    }

    public virtual async Task<TModel> UpdateAsync(TModel instance, CancellationToken cancellationToken)
    {
        await unitOfWork.UpdateAsync(
            await ToEntityAsync(instance, cancellationToken),
            ETag.All,
            TableUpdateMode.Replace,
            cancellationToken
        );

        return instance;
    }

    public virtual Task RemoveAsync(Guid familyId, Guid id, CancellationToken cancellationToken) =>
        unitOfWork.DeleteAsync(keyService.FamilyPartition(familyId), RowKey(id), cancellationToken);

    protected async Task<TEntity> ToEntityAsync(TModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        var entity = mapper.Map<TEntity>(model);
        entity.PartitionKey = keyService.FamilyPartition(model.FamilyId);
        entity.RowKey = RowKey(IdOf(model));
        await ProtectAsync(model.FamilyId, entity, cancellationToken);

        return entity;
    }

    protected async Task<TModel> ToModelAsync(Guid familyId, TEntity entity, CancellationToken cancellationToken)
    {
        if (protector.HasEncryptedColumns<TEntity>())
        {
            protector.Unprotect(entity, await keyRing.GetAsync(familyId, cancellationToken));
        }

        var model = mapper.Map<TModel>(entity);
        model.FamilyId = familyId;

        return model;
    }

    protected async Task ProtectAsync<T>(Guid familyId, T entity, CancellationToken cancellationToken)
        where T : ITableEntity
    {
        if (protector.HasEncryptedColumns<T>())
        {
            protector.Protect(entity, await keyRing.GetAsync(familyId, cancellationToken));
        }
    }
}
