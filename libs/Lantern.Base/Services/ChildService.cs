using System.Globalization;
using Lantern.Base.Logging;
using Lantern.Base.Validation;
using Lantern.Core.Actions;
using Lantern.Core.Exceptions;
using Lantern.Core.Identity;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Extensions.Logging;

namespace Lantern.Base.Services;

internal sealed class ChildService(
    IChildRepository children,
    IFamilyRepository families,
    IIdentityResolver identityResolver,
    IActionPublisher actions,
    IValidator<Child> validator,
    IChildTextNormalizer text,
    TimeProvider clock,
    ILogger<ChildService> logger
) : ServiceBase<Child, IChildRepository>(children, identityResolver, families), IChildService
{
    public override async Task<Child> AddAsync(Child instance, CancellationToken cancellationToken) =>
        (await AddOrGetAsync(instance, cancellationToken)).Child;

    public async Task<(Child Child, bool Created)> AddOrGetAsync(Child child, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.ChildId == Guid.Empty)
        {
            throw new InvalidRequestException("A child id is required.");
        }

        validator.Validate(child);

        var familyId = await FamilyIdAsync(cancellationToken);
        var family = await Repository.CollectionAsync(familyId, cancellationToken);
        if (family.FirstOrDefault(stored => stored.ChildId == child.ChildId) is { } existing)
        {
            return existing.Status == ChildStatus.Deleting ? throw new ChildDeletingException() : (existing, false);
        }

        // Checked here too so a full Family records no action; the repository holds the limit under a race.
        if (family.Count(stored => stored.Status == ChildStatus.Active) >= Child.MaxPerFamily)
        {
            throw new ChildLimitReachedException();
        }

        child.FamilyId = familyId;
        child.Name = text.Name(child.Name);
        child.School = text.School(child.School);
        child.CreatedAt = clock.GetUtcNow();

        // Recorded before the row, so a crash after it still gets the Child its Workspace (D33).
        var workspace = new CreateWorkspacePayload(familyId, child.ChildId, child.ClassLevel);
        var message = await actions.RecordAsync(ActionType.CreateWorkspace, workspace.ActionId(), workspace, cancellationToken);
        await Repository.AddAsync(child, cancellationToken);
        await actions.SendAsync(message, cancellationToken);

        BaseLog.ChildAdded(logger, familyId, child.ChildId);

        return (child, true);
    }

    public override async Task<Child> UpdateAsync(Child instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);

        validator.Validate(instance);

        instance.Name = text.Name(instance.Name);
        instance.School = text.School(instance.School);

        var updated = await base.UpdateAsync(instance, cancellationToken);

        BaseLog.ChildEdited(logger, updated.FamilyId, updated.ChildId);

        return updated;
    }

    // The ledger row is the commit point: once it exists the delete will finish, whatever happens to this request.
    public override async Task RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var familyId = await FamilyIdAsync(cancellationToken);
        // A child already being deleted is still this Family's, so asking again is safe.
        _ = await Repository.SingleAsync(familyId, id, cancellationToken);

        var message = await actions.RecordAsync(
            ActionType.RemoveWorkspace,
            string.Create(CultureInfo.InvariantCulture, $"{familyId:N}_{id:N}"),
            new RemoveWorkspacePayload(familyId, id),
            cancellationToken
        );
        await Repository.MarkDeletingAsync(familyId, id, cancellationToken);
        await actions.SendAsync(message, cancellationToken);

        BaseLog.ChildDeleteRequested(logger, familyId, id);
    }
}
