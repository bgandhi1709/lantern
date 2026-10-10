using Azure.Data.Tables;
using Lantern.Core.Configuration;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Security;
using Lantern.Repository;
using Lantern.Repository.Entities;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Test.Integration.Host;

// One Family with two Parents and ten Children, one per Class, for the two Auth Emulator users with these uids.
// Personal fields are stored as the phone would send them: locked. The seed holds readable stand-ins, because the
// API never reads them. Registration limits (six Children, one Parent) do not apply: the repository is called directly, and the second
// Parent is written straight to the tables.
internal sealed class DevelopmentSeeder(
    IFamilyRepository families,
    IWorkspaceStore workspaces,
    IRowKeyService keyService,
    ICryptoService crypto,
    TableServiceClient tables,
    IOptions<StorageOptions> storage,
    TimeProvider clock
)
{
    internal const string ParentOneUid = "dev-parent-1";
    internal const string ParentTwoUid = "dev-parent-2";
    private const int ClassCount = 10;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var first = await families.FindParentAsync(ParentOneUid, cancellationToken) ?? await RegisterFirstAsync(cancellationToken);

        if (await families.FindParentAsync(ParentTwoUid, cancellationToken) is null)
        {
            await AddSecondParentAsync(first.FamilyId, cancellationToken);
        }
    }

    private async Task<Parent> RegisterFirstAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var family = new Family
        {
            FamilyId = Guid.NewGuid(),
            Region = "Gujarat",
            Board = BoardType.Cbse,
            PassphraseWrappedKey = "dev-passphrase-wrapped-key",
            PassphraseSalt = "dev-passphrase-salt",
            RecoveryWrappedKey = "dev-recovery-wrapped-key",
            RecoverySalt = "dev-recovery-salt",
            CreatedAt = now,
        };
        var parent = new Parent
        {
            ParentId = Guid.NewGuid(),
            FamilyId = family.FamilyId,
            Name = "locked:Dev Parent One",
            Email = "locked:dev-parent-1@lantern.local",
            Language = "en",
            ConsentVersion = "dev",
            ConsentAt = now,
            CreatedAt = now,
        };
        List<Child> children =
        [
            .. Enumerable
                .Range(1, ClassCount)
                .Select(level => new Child
                {
                    FamilyId = family.FamilyId,
                    ChildId = Guid.NewGuid(),
                    Name = $"locked:Dev Child {level}",
                    School = "locked:Lantern Dev School",
                    ClassLevel = level,
                    BirthYear = $"locked:{now.Year - (level + 5)}",
                    Position = level - 1,
                    CreatedAt = now,
                    Status = ChildStatus.Active,
                }),
        ];

        foreach (var child in children)
        {
            await workspaces.CreateAsync(family.FamilyId, child.ChildId, child.ClassLevel, cancellationToken);
        }

        await families.RegisterAsync(ParentOneUid, family, parent, children, cancellationToken);

        return parent;
    }

    private async Task AddSecondParentAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var uidHash = crypto.Hash(ParentTwoUid);
        var now = clock.GetUtcNow();
        var parentId = Guid.NewGuid();
        var membership = new MembershipEntity
        {
            PartitionKey = keyService.FamilyPartition(familyId),
            RowKey = keyService.MembershipRowKey(uidHash),
            ParentId = parentId,
            CreatedAt = now,
        };
        var profile = new ParentEntity
        {
            PartitionKey = uidHash,
            RowKey = keyService.ProfileRowKey,
            ParentId = parentId,
            FamilyId = familyId,
            Name = "locked:Dev Parent Two",
            Email = "locked:dev-parent-2@lantern.local",
            Language = "gu",
            ConsentVersion = "dev",
            ConsentAt = now,
            CreatedAt = now,
        };
        // Same order as registration: the Parent's profile row is the commit point.
        await tables.GetTableClient(storage.Value.FamiliesTable).AddEntityAsync(membership, cancellationToken);
        await tables.GetTableClient(storage.Value.ParentsTable).AddEntityAsync(profile, cancellationToken);
    }
}
