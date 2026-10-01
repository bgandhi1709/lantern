using Lantern.Api.Auth;
using Lantern.Api.Contracts;
using Lantern.Api.Exceptions;
using Lantern.Api.Logging;
using Lantern.Api.Models;
using Lantern.Api.Repository;
using Lantern.Api.Services.Interfaces;
using Lantern.Api.Validation;

namespace Lantern.Api.Services;

internal sealed class RegistrationService(
    IFamilyRepository families,
    IUidHasher hasher,
    IFieldCipher cipher,
    IFamilyKeyService familyKeys,
    IClassSpaceStore classSpaces,
    IValidator<RegisterBody> validator,
    IChildTextNormalizer text,
    TimeProvider clock,
    ILogger<RegistrationService> logger
) : IRegistrationService
{
    public async Task<FamilyView> RegisterAsync(
        Caller caller,
        RegisterBody body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(body);

        var now = clock.GetUtcNow();
        validator.Validate(body);

        var partitionKey = hasher.Hash(caller.Uid);

        // Before any write, so a repeat register leaves nothing behind; the profile Add still settles a real race.
        if (await families.GetAsync(partitionKey, cancellationToken) is not null)
        {
            throw new AlreadyRegisteredException();
        }

        var familyId = Guid.NewGuid();
        var familyPartition = FamilyRepository.FamilyPartition(familyId);
        var (dek, wrappedFieldKey) = await familyKeys.GenerateAsync(cancellationToken);

        var family = new FamilyRecord(familyId, body.Region.Trim(), wrappedFieldKey, KeyScheme.KeyVault, now);
        var parent = new ParentProfile(
            partitionKey,
            Guid.NewGuid(),
            familyId,
            cipher.Protect(dek, caller.Name, partitionKey, FamilyRepository.ProfileRowKey, "name"),
            cipher.Protect(dek, caller.Email, partitionKey, FamilyRepository.ProfileRowKey, "email"),
            body.Language,
            body.Consent.NoticeVersion.Trim(),
            now,
            now
        );

        var views = new List<ChildView>(body.Children.Count);
        var records = new List<ChildRecord>(body.Children.Count);

        for (var position = 0; position < body.Children.Count; position++)
        {
            var child = body.Children[position];
            var childId = Guid.NewGuid();
            var rowKey = FamilyRepository.ChildRowKey(childId);
            var name = text.Name(child.Name);
            var school = text.School(child.School);

            records.Add(
                new ChildRecord(
                    childId,
                    cipher.Protect(dek, name, familyPartition, rowKey, "name"),
                    school is null ? null : cipher.Protect(dek, school, familyPartition, rowKey, "school"),
                    child.ClassLevel,
                    child.BirthYear,
                    position,
                    now
                )
            );
            views.Add(new ChildView(childId, name, school, child.ClassLevel, child.BirthYear));
        }

        // Before the profile row, which is the commit point: a failure here leaves the caller unregistered.
        foreach (var child in records)
        {
            await classSpaces.StartAsync(familyId, child.ChildId, child.ClassLevel, cancellationToken);
        }

        await families.RegisterAsync(parent, family, records, cancellationToken);

        Log.FamilyRegistered(logger, familyId, views.Count);

        return new FamilyView(
            familyId,
            family.Region,
            new ParentView(parent.ParentId, caller.Name, caller.Email, parent.Language, parent.ConsentVersion, now),
            views
        );
    }

    public async Task<FamilyView> GetAsync(Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var partitionKey = hasher.Hash(caller.Uid);
        var stored =
            await families.GetAsync(partitionKey, cancellationToken) ?? throw new NotRegisteredException();

        var parent = stored.Parent;
        var familyPartition = FamilyRepository.FamilyPartition(stored.Family.FamilyId);
        var dek = await familyKeys.UnwrapAsync(stored.Family.WrappedFieldKey, cancellationToken);

        return new FamilyView(
            stored.Family.FamilyId,
            stored.Family.Region,
            new ParentView(
                parent.ParentId,
                cipher.Unprotect(dek, parent.NameCipher, partitionKey, FamilyRepository.ProfileRowKey, "name"),
                cipher.Unprotect(dek, parent.EmailCipher, partitionKey, FamilyRepository.ProfileRowKey, "email"),
                parent.Language,
                parent.ConsentVersion,
                parent.ConsentAt
            ),
            [
                .. stored.Children.Where(child => child.Status == ChildStatus.Active).Select(child =>
                {
                    var rowKey = FamilyRepository.ChildRowKey(child.ChildId);

                    return new ChildView(
                        child.ChildId,
                        cipher.Unprotect(dek, child.NameCipher, familyPartition, rowKey, "name"),
                        child.SchoolCipher is null
                            ? null
                            : cipher.Unprotect(dek, child.SchoolCipher, familyPartition, rowKey, "school"),
                        child.ClassLevel,
                        child.BirthYear
                    );
                }),
            ]
        );
    }
}
