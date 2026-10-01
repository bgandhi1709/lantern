using Azure.Data.Tables;
using Lantern.Api.Auth;
using Lantern.Api.Configuration;
using Lantern.Api.Contracts;
using Lantern.Api.Repository;
using Lantern.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Test.Integration.Host;

// One Family with two Parents and ten Children, one per Class, for the two Auth Emulator users with these uids.
// Registration limits (six Children, one Parent) do not apply: the second Parent is written straight to the tables.
internal sealed class DevelopmentSeeder(
    IRegistrationService registration,
    IFamilyRepository families,
    IFamilyKeyService familyKeys,
    IFieldCipher cipher,
    IRowKeys keys,
    IUidHasher hasher,
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
        var first = hasher.Hash(ParentOneUid);
        var stored = await families.GetAsync(first, cancellationToken);

        if (stored is null)
        {
            await registration.RegisterAsync(
                new Caller(ParentOneUid, "Dev Parent One", "dev-parent-1@lantern.local"),
                FamilyBody(clock.GetUtcNow().Year),
                cancellationToken
            );
            stored = await families.GetAsync(first, cancellationToken);
        }

        var second = hasher.Hash(ParentTwoUid);
        if (stored is not null && await families.GetAsync(second, cancellationToken) is null)
        {
            await AddSecondParentAsync(stored.Family, second, cancellationToken);
        }
    }

    private async Task AddSecondParentAsync(Models.FamilyRecord family, string partitionKey, CancellationToken cancellationToken)
    {
        var dek = await familyKeys.UnwrapAsync(family.WrappedFieldKey, cancellationToken);
        var now = clock.GetUtcNow();
        var parentId = Guid.NewGuid();

        var membership = new TableEntity(keys.FamilyPartition(family.FamilyId), $"parent_{partitionKey}")
        {
            ["ParentId"] = parentId,
            ["CreatedAt"] = now,
        };
        var profile = new TableEntity(partitionKey, FamilyRepository.ProfileRowKey)
        {
            ["ParentId"] = parentId,
            ["FamilyId"] = family.FamilyId,
            ["NameCipher"] = cipher.Protect(dek, "Dev Parent Two", partitionKey, FamilyRepository.ProfileRowKey, "name"),
            ["EmailCipher"] = cipher.Protect(dek, "dev-parent-2@lantern.local", partitionKey, FamilyRepository.ProfileRowKey, "email"),
            ["Language"] = "gu",
            ["ConsentVersion"] = "dev",
            ["ConsentAt"] = now,
            ["CreatedAt"] = now,
        };

        // Same order as registration: the Parent's profile row is the commit point.
        await tables.GetTableClient(storage.Value.FamiliesTable).AddEntityAsync(membership, cancellationToken);
        await tables.GetTableClient(storage.Value.ParentsTable).AddEntityAsync(profile, cancellationToken);
    }

    private static RegisterBody FamilyBody(int year) =>
        new()
        {
            Region = "Gujarat",
            Language = "en",
            Consent = new ConsentBody { Accepted = true, NoticeVersion = "dev" },
            Children =
            [
                .. Enumerable
                    .Range(1, ClassCount)
                    .Select(level => new ChildBody
                    {
                        Name = $"Dev Child {level}",
                        ClassLevel = level,
                        BirthYear = year - (level + 5),
                        School = "Lantern Dev School",
                    }),
            ],
        };
}
