using Azure;
using Azure.Data.Tables;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Repository.Entities;
using Lantern.Repository.UnitOfWork;
using MapsterMapper;

namespace Lantern.Repository;

internal sealed class ChildRepository(
    IUnitOfWork<ChildEntity> unitOfWork,
    IUnitOfWork<FamilyEntity> families,
    IMapper mapper,
    IRowKeyService keyService
) : BaseRepository<Child, ChildEntity>(unitOfWork, mapper, keyService), IChildRepository
{
    private const int MaxEditAttempts = 3;

    protected override string RowKeyPrefix => KeyService.ChildRowPrefix;

    protected override string RowKey(Guid id) => KeyService.ChildRowKey(id);

    protected override Guid IdOf(Child model) => model.ChildId;

    public override async Task<IReadOnlyList<Child>> CollectionAsync(Guid familyId, CancellationToken cancellationToken) =>
        [.. (await base.CollectionAsync(familyId, cancellationToken)).OrderBy(child => child.Position)];

    public override async Task<Child> AddAsync(Child instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var partition = KeyService.FamilyPartition(instance.FamilyId);
        // The family row first: an add that lands between the two reads is counted and still breaks this ETag.
        var family =
            await families.SingleOrNullAsync(partition, KeyService.FamilyRowKey, cancellationToken)
            ?? throw new NotFoundException("family");
        var children = await UnitOfWork.PartitionAsync(partition, KeyService.ChildRowPrefix, cancellationToken);

        if (children.Count(child => child.Status == nameof(ChildStatus.Active)) >= Child.MaxPerFamily)
        {
            throw new ChildLimitReachedException();
        }

        instance.Position = children.Select(child => child.Position).DefaultIfEmpty(-1).Max() + 1;
        instance.Status = ChildStatus.Active;

        try
        {
            await UnitOfWork.SubmitAsync(
                [
                    new(TableTransactionActionType.Add, ToEntity(instance)),
                    new(TableTransactionActionType.UpdateMerge, new TableEntity(partition, KeyService.FamilyRowKey)
                    {
                        [nameof(FamilyEntity.ChildrenChangedAt)] = instance.CreatedAt,
                    }, family.ETag),
                ],
                cancellationToken
            );
        }
        catch (TableTransactionFailedException ex) when (ex.Status is 409 or 412)
        {
            throw new FamilyChangedException();
        }

        return instance;
    }

    public override async Task<Child> UpdateAsync(Child instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var changed = ToEntity(instance);

        // The row's own ETag keeps an edit from writing a stale Status over a delete that just marked it.
        for (var attempt = 0; attempt < MaxEditAttempts; attempt++)
        {
            var row = await UnitOfWork.SingleOrNullAsync(changed.PartitionKey, changed.RowKey, cancellationToken);
            if (row is null || row.Status == nameof(ChildStatus.Deleting))
            {
                throw new NotFoundException("child");
            }

            row.NameLocked = changed.NameLocked;
            row.SchoolLocked = changed.SchoolLocked;
            row.BirthYearLocked = changed.BirthYearLocked;

            try
            {
                await UnitOfWork.UpdateAsync(row, row.ETag, TableUpdateMode.Replace, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                continue;
            }

            instance.ClassLevel = row.ClassLevel;
            instance.Position = row.Position;
            instance.CreatedAt = row.CreatedAt;
            instance.Status = ChildStatus.Active;

            return instance;
        }

        throw new FamilyChangedException();
    }

    public async Task MarkDeletingAsync(Guid familyId, Guid childId, CancellationToken cancellationToken)
    {
        try
        {
            await UnitOfWork.UpdateAsync(
                new TableEntity(KeyService.FamilyPartition(familyId), RowKey(childId))
                {
                    [nameof(ChildEntity.Status)] = nameof(ChildStatus.Deleting),
                },
                ETag.All,
                TableUpdateMode.Merge,
                cancellationToken
            );
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // A finished delete already removed it.
        }
    }

    public async Task<ChildStatus?> StatusAsync(Guid familyId, Guid childId, CancellationToken cancellationToken) =>
        await UnitOfWork.SingleOrNullAsync(KeyService.FamilyPartition(familyId), RowKey(childId), cancellationToken) is { } row
            ? Enum.Parse<ChildStatus>(row.Status)
            : null;
}
