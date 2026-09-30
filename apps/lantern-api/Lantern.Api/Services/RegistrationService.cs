using Lantern.Api.Auth;
using Lantern.Api.Contracts;
using Lantern.Api.Exceptions;
using Lantern.Api.Logging;
using Lantern.Api.Models;
using Lantern.Api.Repository;
using Lantern.Api.Services.Interfaces;

namespace Lantern.Api.Services;

internal sealed class RegistrationService(
    IParentRepository parents,
    IUidHasher hasher,
    IFieldCipher cipher,
    IFamilyKeyService familyKeys,
    TimeProvider clock,
    ILogger<RegistrationService> logger
) : IRegistrationService
{
    private const int MinChildAge = 3;
    private const int MaxChildAge = 18;

    public async Task<FamilyView> RegisterAsync(
        Caller caller,
        RegisterBody body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(body);

        var now = clock.GetUtcNow();
        Validate(body, now.Year);

        var partitionKey = hasher.Hash(caller.Uid);
        var familyId = Guid.NewGuid();
        var (dek, wrappedFieldKey) = await familyKeys.GenerateAsync(cancellationToken);

        var profile = new ParentProfile(
            partitionKey,
            familyId,
            cipher.Protect(dek, caller.Name, partitionKey, ParentRepository.ProfileRowKey, "name"),
            cipher.Protect(dek, caller.Email, partitionKey, ParentRepository.ProfileRowKey, "email"),
            wrappedFieldKey,
            body.Region.Trim(),
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
            var rowKey = ParentRepository.ChildRowKey(childId);
            var name = child.Name.Trim();
            var school = string.IsNullOrWhiteSpace(child.School) ? null : child.School.Trim();

            records.Add(
                new ChildRecord(
                    childId,
                    cipher.Protect(dek, name, partitionKey, rowKey, "name"),
                    school is null ? null : cipher.Protect(dek, school, partitionKey, rowKey, "school"),
                    child.ClassLevel,
                    child.BirthYear,
                    position,
                    now
                )
            );
            views.Add(new ChildView(childId, name, school, child.ClassLevel, child.BirthYear));
        }

        if (!await parents.TryRegisterAsync(profile, records, cancellationToken))
        {
            throw new AlreadyRegisteredException();
        }

        Log.FamilyRegistered(logger, familyId, views.Count);

        return new FamilyView(
            familyId,
            caller.Name,
            caller.Email,
            profile.Region,
            profile.Language,
            profile.ConsentVersion,
            now,
            views
        );
    }

    public async Task<FamilyView> GetAsync(Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var partitionKey = hasher.Hash(caller.Uid);
        var stored =
            await parents.GetAsync(partitionKey, cancellationToken) ?? throw new NotRegisteredException();

        var profile = stored.Profile;
        var profileKey = ParentRepository.ProfileRowKey;
        var dek = await familyKeys.UnwrapAsync(profile.WrappedFieldKey, cancellationToken);

        return new FamilyView(
            profile.FamilyId,
            cipher.Unprotect(dek, profile.NameCipher, partitionKey, profileKey, "name"),
            cipher.Unprotect(dek, profile.EmailCipher, partitionKey, profileKey, "email"),
            profile.Region,
            profile.Language,
            profile.ConsentVersion,
            profile.ConsentAt,
            [
                .. stored.Children.Select(child =>
                {
                    var rowKey = ParentRepository.ChildRowKey(child.ChildId);

                    return new ChildView(
                        child.ChildId,
                        cipher.Unprotect(dek, child.NameCipher, partitionKey, rowKey, "name"),
                        child.SchoolCipher is null
                            ? null
                            : cipher.Unprotect(dek, child.SchoolCipher, partitionKey, rowKey, "school"),
                        child.ClassLevel,
                        child.BirthYear
                    );
                }),
            ]
        );
    }

    private static void Validate(RegisterBody body, int currentYear)
    {
        if (!body.Consent.Accepted)
        {
            throw new InvalidRegistrationException("Consent must be accepted to register.");
        }

        foreach (var child in body.Children)
        {
            if (child.BirthYear < currentYear - MaxChildAge || child.BirthYear > currentYear - MinChildAge)
            {
                throw new InvalidRegistrationException(
                    $"A child's birth year must be between {currentYear - MaxChildAge} and {currentYear - MinChildAge}."
                );
            }
        }
    }
}
