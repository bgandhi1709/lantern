using Azure;
using Azure.Data.Tables;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Security;
using Lantern.Repository.Entities;
using Lantern.Repository.UnitOfWork;
using MapsterMapper;

namespace Lantern.Repository;

// A Family's rows (family, parent memberships, children) share its partition; each Parent's profile lives in the
// parents table under the hash of their uid (ADR-0001).
internal sealed class FamilyRepository(
    IUnitOfWork<FamilyEntity> unitOfWork,
    IUnitOfWork<ParentEntity> parents,
    ICryptoService crypto,
    IMapper mapper,
    IRowKeyService keyService
) : BaseRepository<Family, FamilyEntity>(unitOfWork, mapper, keyService), IFamilyRepository
{
    // Table Storage's limit on actions in one transaction.
    private const int MaxBatch = 100;

    protected override string RowKeyPrefix => KeyService.FamilyRowKey;

    protected override string RowKey(Guid id) => KeyService.FamilyRowKey;

    protected override Guid IdOf(Family model) => model.FamilyId;

    protected override LanternErrorCode NotFoundCode => LanternErrorCode.FamilyNotFound;

    public async Task<Parent> FindParentAsync(string uid, CancellationToken cancellationToken)
    {
        var profile = await parents.SingleOrNullAsync(crypto.Hash(uid), KeyService.ProfileRowKey, cancellationToken);

        return profile is null ? null : Mapper.Map<Parent>(profile);
    }

    public async Task RegisterAsync(
        string uid,
        Family family,
        Parent parent,
        IReadOnlyList<Child> children,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(children);

        var uidHash = crypto.Hash(uid);
        // Lookup first so a repeat register writes nothing; the profile Add below still settles a real race.
        if (await parents.SingleOrNullAsync(uidHash, KeyService.ProfileRowKey, cancellationToken) is not null)
        {
            throw new LanternException(LanternErrorCode.AlreadyRegistered);
        }

        var familyRow = ToEntity(family);

        var partition = KeyService.FamilyPartition(family.FamilyId);
        List<TableTransactionAction> rows =
        [
            new(TableTransactionActionType.Add, familyRow),
            new(
                TableTransactionActionType.Add,
                new MembershipEntity
                {
                    PartitionKey = partition,
                    RowKey = KeyService.MembershipRowKey(uidHash),
                    ParentId = parent.ParentId,
                    CreatedAt = parent.CreatedAt,
                }
            ),
        ];
        foreach (var child in children)
        {
            var childRow = Mapper.Map<ChildEntity>(child);
            childRow.PartitionKey = partition;
            childRow.RowKey = KeyService.ChildRowKey(child.ChildId);
            rows.Add(new TableTransactionAction(TableTransactionActionType.Add, childRow));
        }

        var profile = Mapper.Map<ParentEntity>(parent);
        profile.PartitionKey = uidHash;
        profile.RowKey = KeyService.ProfileRowKey;

        try
        {
            await UnitOfWork.SubmitAsync(rows, cancellationToken);
        }
        catch (TableTransactionFailedException ex) when (ex.Status == 409)
        {
            // The phone picks the Family id, so a taken id is a refusal, never an overwrite.
            throw new LanternException(LanternErrorCode.FamilyIdTaken);
        }

        try
        {
            await parents.AddAsync(profile, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Lost the race: this attempt's family rows are unreachable, so take them back.
            await UnitOfWork.SubmitAsync(
                rows.Select(row => new TableTransactionAction(
                    TableTransactionActionType.Delete,
                    new TableEntity(row.Entity.PartitionKey, row.Entity.RowKey)
                )),
                cancellationToken
            );

            throw new LanternException(LanternErrorCode.AlreadyRegistered);
        }
    }

    public async Task RemoveParentsAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var memberships = await UnitOfWork.PartitionAsync(
            KeyService.FamilyPartition(familyId),
            KeyService.MembershipRowPrefix,
            cancellationToken
        );

        // Each profile is its own partition in the parents table, so they cannot share a batch.
        await Task.WhenAll(
            memberships.Select(membership =>
                RemoveProfileAsync(familyId, membership.RowKey[KeyService.MembershipRowPrefix.Length..], cancellationToken)
            )
        );
    }

    public async Task EraseAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await RemoveParentsAsync(familyId, cancellationToken);

        var partition = KeyService.FamilyPartition(familyId);
        try
        {
            // Crypto-shredding first: without the wrapped keys, whatever survives a failed sweep can never be unlocked.
            await UnitOfWork.UpdateAsync(
                new TableEntity(partition, KeyService.FamilyRowKey)
                {
                    [nameof(FamilyEntity.PassphraseWrappedKey)] = string.Empty,
                    [nameof(FamilyEntity.RecoveryWrappedKey)] = string.Empty,
                },
                ETag.All,
                TableUpdateMode.Merge,
                cancellationToken
            );
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // A previous attempt already deleted the family row.
        }

        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partition}");
        IReadOnlyList<FamilyEntity> rows;
        while ((rows = await UnitOfWork.QueryAsync(filter, cancellationToken)).Count > 0)
        {
            try
            {
                await Task.WhenAll(
                    rows.Chunk(MaxBatch)
                        .Select(batch =>
                            UnitOfWork.SubmitAsync(
                                batch.Select(row => new TableTransactionAction(TableTransactionActionType.Delete, row, ETag.All)),
                                cancellationToken
                            )
                        )
                );
            }
            catch (TableTransactionFailedException ex) when (ex.Status == 404)
            {
                // A concurrent delivery deleted some of these rows; read what is left.
            }
        }
    }

    private async Task RemoveProfileAsync(Guid familyId, string uidHash, CancellationToken cancellationToken)
    {
        // The Parent may have registered again since: that profile belongs to the new Family.
        if (await parents.SingleOrNullAsync(uidHash, KeyService.ProfileRowKey, cancellationToken) is { } profile
            && profile.FamilyId == familyId)
        {
            await parents.DeleteAsync(uidHash, KeyService.ProfileRowKey, cancellationToken);
        }
    }
}
