using Azure;
using Azure.Data.Tables;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Security;
using Lantern.Repository.Entities;
using Lantern.Repository.Security;
using Lantern.Repository.UnitOfWork;
using MapsterMapper;

namespace Lantern.Repository;

// A Family's rows (family, parent memberships, children) share its partition; each Parent's profile lives in the
// parents table under the hash of their uid (ADR-0001).
internal sealed class FamilyRepository(
    IUnitOfWork<FamilyEntity> unitOfWork,
    IUnitOfWork<ParentEntity> parents,
    IFamilyKeyRing keyRing,
    IFieldProtector protector,
    ICryptoService crypto,
    IMapper mapper,
    IRowKeyService keyService
) : BaseRepository<Family, FamilyEntity>(unitOfWork, keyRing, protector, mapper, keyService), IFamilyRepository
{
    protected override string RowKeyPrefix => KeyService.FamilyRowKey;

    protected override string RowKey(Guid id) => KeyService.FamilyRowKey;

    protected override Guid IdOf(Family model) => model.FamilyId;

    public async Task<Parent?> FindParentAsync(string uid, CancellationToken cancellationToken)
    {
        var profile = await parents.SingleOrNullAsync(crypto.Hash(uid), KeyService.ProfileRowKey, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        Protector.Unprotect(profile, await KeyRing.GetAsync(profile.FamilyId, cancellationToken));

        return Mapper.Map<Parent>(profile);
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
            throw new AlreadyRegisteredException();
        }

        var (_, wrappedKey) = await KeyRing.CreateAsync(family.FamilyId, cancellationToken);
        var familyRow = await ToEntityAsync(family, cancellationToken);
        familyRow.WrappedFieldKey = wrappedKey;

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
            await ProtectAsync(family.FamilyId, childRow, cancellationToken);
            rows.Add(new(TableTransactionActionType.Add, childRow));
        }

        var profile = Mapper.Map<ParentEntity>(parent);
        profile.PartitionKey = uidHash;
        profile.RowKey = KeyService.ProfileRowKey;
        await ProtectAsync(family.FamilyId, profile, cancellationToken);

        await UnitOfWork.SubmitAsync(rows, cancellationToken);

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

            throw new AlreadyRegisteredException();
        }
    }
}
