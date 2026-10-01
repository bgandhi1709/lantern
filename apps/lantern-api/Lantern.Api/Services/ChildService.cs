using Lantern.Api.Auth;
using Lantern.Api.Contracts;
using Lantern.Api.Exceptions;
using Lantern.Api.Logging;
using Lantern.Api.Models;
using Lantern.Api.Repository;
using Lantern.Api.Services.Interfaces;
using Lantern.Api.Validation;

namespace Lantern.Api.Services;

// Every call finds the Family from the caller's token. A child id is only ever looked up inside that Family,
// so another Family's id is indistinguishable from an unknown one.
internal sealed class ChildService(
    IFamilyRepository families,
    IChildDeletionStore deletions,
    IUidHasher hasher,
    IFieldCipher cipher,
    IRowKeys keys,
    IFamilyKeyService familyKeys,
    IClassSpaceStore classSpaces,
    IValidator<AddChildBody> addValidator,
    IValidator<ChildDetailsBody> editValidator,
    IChildTextNormalizer text,
    TimeProvider clock,
    ILogger<ChildService> logger
) : IChildService
{
    public async Task<AddChildResult> AddAsync(Caller caller, AddChildBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(body);

        var now = clock.GetUtcNow();
        addValidator.Validate(body);

        var family = await LoadAsync(caller, cancellationToken);
        var familyPartition = keys.FamilyPartition(family.Family.FamilyId);
        var dek = await familyKeys.UnwrapAsync(family.Family.WrappedFieldKey, cancellationToken);

        if (family.Children.FirstOrDefault(child => child.ChildId == body.ChildId) is { } existing)
        {
            return existing.Status == ChildStatus.Deleting
                ? throw new ChildDeletingException()
                : new AddChildResult(ToView(existing, dek, familyPartition), Created: false);
        }

        if (family.Children.Count(child => child.Status == ChildStatus.Active) >= ChildLimits.MaxChildren)
        {
            throw new ChildLimitReachedException();
        }

        var rowKey = keys.ChildRowKey(body.ChildId);
        var name = text.Name(body.Name);
        var school = text.School(body.School);
        var record = new ChildRecord(
            body.ChildId,
            cipher.Protect(dek, name, familyPartition, rowKey, "name"),
            school is null ? null : cipher.Protect(dek, school, familyPartition, rowKey, "school"),
            body.ClassLevel,
            body.BirthYear,
            family.Children.Select(child => child.Position).DefaultIfEmpty(-1).Max() + 1,
            now
        );

        // Before the row, as at registration: a failed start leaves no Child behind.
        await classSpaces.StartAsync(family.Family.FamilyId, record.ChildId, record.ClassLevel, cancellationToken);
        await families.AddChildAsync(family.Family.FamilyId, record, family.FamilyETag, cancellationToken);

        Log.ChildAdded(logger, family.Family.FamilyId, record.ChildId);

        return new AddChildResult(new ChildView(record.ChildId, name, school, record.ClassLevel, record.BirthYear), true);
    }

    public async Task<ChildView> EditAsync(
        Caller caller,
        Guid childId,
        ChildDetailsBody body,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(body);

        editValidator.Validate(body);

        var family = await LoadAsync(caller, cancellationToken);
        var child = FindActive(family, childId);
        var familyPartition = keys.FamilyPartition(family.Family.FamilyId);
        var dek = await familyKeys.UnwrapAsync(family.Family.WrappedFieldKey, cancellationToken);
        var rowKey = keys.ChildRowKey(childId);
        var name = text.Name(body.Name);
        var school = text.School(body.School);

        await families.UpdateChildAsync(
            family.Family.FamilyId,
            childId,
            cipher.Protect(dek, name, familyPartition, rowKey, "name"),
            school is null ? null : cipher.Protect(dek, school, familyPartition, rowKey, "school"),
            body.BirthYear,
            cancellationToken
        );

        Log.ChildEdited(logger, family.Family.FamilyId, childId);

        return new ChildView(childId, name, school, child.ClassLevel, body.BirthYear);
    }

    public async Task DeleteAsync(Caller caller, Guid childId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var family = await LoadAsync(caller, cancellationToken);
        // A child already being deleted is still this Family's, so asking again is safe.
        if (family.Children.All(child => child.ChildId != childId))
        {
            throw new ChildNotFoundException();
        }

        await deletions.RequestAsync(family.Family.FamilyId, childId, clock.GetUtcNow(), cancellationToken);

        Log.ChildDeleteRequested(logger, family.Family.FamilyId, childId);
    }

    private async Task<FamilyAggregate> LoadAsync(Caller caller, CancellationToken cancellationToken) =>
        await families.GetAsync(hasher.Hash(caller.Uid), cancellationToken) ?? throw new NotRegisteredException();

    private static ChildRecord FindActive(FamilyAggregate family, Guid childId) =>
        family.Children.FirstOrDefault(child => child.ChildId == childId && child.Status == ChildStatus.Active)
        ?? throw new ChildNotFoundException();

    private ChildView ToView(ChildRecord child, byte[] dek, string familyPartition)
    {
        var rowKey = keys.ChildRowKey(child.ChildId);

        return new ChildView(
            child.ChildId,
            cipher.Unprotect(dek, child.NameCipher, familyPartition, rowKey, "name"),
            child.SchoolCipher is null
                ? null
                : cipher.Unprotect(dek, child.SchoolCipher, familyPartition, rowKey, "school"),
            child.ClassLevel,
            child.BirthYear
        );
    }
}
