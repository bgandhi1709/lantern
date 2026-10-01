using Lantern.Api.Auth;
using Lantern.Api.Contracts;
using Lantern.Api.Models;
using Lantern.Api.Repository;
using Lantern.Api.Services.Interfaces;

namespace Lantern.Api.Services;

// Local Docker only (`dotnet Lantern.Api.dll seed`): one Family with two Parents and ten Children, one per Class,
// for the two Auth Emulator users with these uids. Registration rules (six Children, one Parent) do not apply here.
internal sealed class DevelopmentSeeder(
    IRegistrationService registration,
    IFamilyRepository families,
    IFamilyKeyService familyKeys,
    IFieldCipher cipher,
    IUidHasher hasher,
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
            await JoinSecondParentAsync(stored.Family, second, cancellationToken);
        }
    }

    private async Task JoinSecondParentAsync(FamilyRecord family, string partitionKey, CancellationToken cancellationToken)
    {
        var dek = await familyKeys.UnwrapAsync(family.WrappedFieldKey, cancellationToken);
        var now = clock.GetUtcNow();

        await families.JoinAsync(
            new ParentProfile(
                partitionKey,
                Guid.NewGuid(),
                family.FamilyId,
                cipher.Protect(dek, "Dev Parent Two", partitionKey, FamilyRepository.ProfileRowKey, "name"),
                cipher.Protect(dek, "dev-parent-2@lantern.local", partitionKey, FamilyRepository.ProfileRowKey, "email"),
                "gu",
                "dev",
                now,
                now
            ),
            cancellationToken
        );
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
