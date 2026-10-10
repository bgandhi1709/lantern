using Azure;
using Azure.Data.Tables;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Repository.UnitOfWork;
using MapsterMapper;

namespace Lantern.Repository;

// Maps a service model to its entity and back. Locked fields pass through untouched (D66). A model type needs only
// its row key; it overrides the rest only where its storage rules differ.
internal abstract class BaseRepository<TModel, TEntity>(
    IUnitOfWork<TEntity> unitOfWork,
    IMapper mapper,
    IRowKeyService keyService
) : IRepositoryBase<TModel>
    where TModel : class, IFamilyModel
    where TEntity : class, ITableEntity, new()
{
    protected IUnitOfWork<TEntity> UnitOfWork => unitOfWork;

    protected IRowKeyService KeyService => keyService;

    protected IMapper Mapper => mapper;

    /// <summary>The prefix shared by the row keys of this type, so a partition read returns only these rows.</summary>
    protected abstract string RowKeyPrefix { get; }

    protected abstract string RowKey(Guid id);

    protected abstract Guid IdOf(TModel model);

    public virtual async Task<TModel?> SingleOrNullAsync(Guid familyId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.SingleOrNullAsync(keyService.FamilyPartition(familyId), RowKey(id), cancellationToken);

        return entity is null ? null : ToModel(familyId, entity);
    }

    public virtual async Task<TModel> SingleAsync(Guid familyId, Guid id, CancellationToken cancellationToken) =>
        await SingleOrNullAsync(familyId, id, cancellationToken)
        ?? throw new NotFoundException(typeof(TModel).Name.ToLowerInvariant());

    public virtual async Task<IReadOnlyList<TModel>> CollectionAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var models = new List<TModel>();
        foreach (var entity in await unitOfWork.PartitionAsync(keyService.FamilyPartition(familyId), RowKeyPrefix, cancellationToken))
        {
            models.Add(ToModel(familyId, entity));
        }

        return models;
    }

    public virtual async Task<TModel> AddAsync(TModel instance, CancellationToken cancellationToken)
    {
        await unitOfWork.AddAsync(ToEntity(instance), cancellationToken);

        return instance;
    }

    public virtual async Task<TModel> UpdateAsync(TModel instance, CancellationToken cancellationToken)
    {
        await unitOfWork.UpdateAsync(
            ToEntity(instance),
            ETag.All,
            TableUpdateMode.Replace,
            cancellationToken
        );

        return instance;
    }

    public virtual Task RemoveAsync(Guid familyId, Guid id, CancellationToken cancellationToken) =>
        unitOfWork.DeleteAsync(keyService.FamilyPartition(familyId), RowKey(id), cancellationToken);

    protected TEntity ToEntity(TModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var entity = mapper.Map<TEntity>(model);
        entity.PartitionKey = keyService.FamilyPartition(model.FamilyId);
        entity.RowKey = RowKey(IdOf(model));

        return entity;
    }

    protected TModel ToModel(Guid familyId, TEntity entity)
    {
        var model = mapper.Map<TModel>(entity);
        model.FamilyId = familyId;

        return model;
    }
}
