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

internal sealed class FamilyService(
    IFamilyRepository families,
    IChildRepository children,
    IActionPublisher actions,
    IIdentityResolver identityResolver,
    IValidator<Registration> validator,
    IChildTextNormalizer text,
    TimeProvider clock,
    ILogger<FamilyService> logger
) : ServiceBase<Family, IFamilyRepository>(families, identityResolver, families), IFamilyService
{
    public async Task<Family> RegisterAsync(Registration registration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        validator.Validate(registration);

        var identity = Identity;
        // Before any write, so a repeat register leaves nothing behind; the repository still settles a real race.
        if (await Families.FindParentAsync(identity.Uid, cancellationToken) is not null)
        {
            throw new AlreadyRegisteredException();
        }

        var now = clock.GetUtcNow();
        var family = new Family
        {
            FamilyId = Guid.NewGuid(),
            Region = registration.Region.Trim(),
            CreatedAt = now,
        };
        var parent = new Parent
        {
            ParentId = Guid.NewGuid(),
            FamilyId = family.FamilyId,
            Name = identity.Name,
            Email = identity.Email,
            Language = registration.Language,
            ConsentVersion = registration.ConsentNoticeVersion.Trim(),
            ConsentAt = now,
            CreatedAt = now,
        };
        Child[] added =
        [
            .. registration.Children.Select(
                (child, position) =>
                    new Child
                    {
                        FamilyId = family.FamilyId,
                        ChildId = Guid.NewGuid(),
                        Name = text.Name(child.Name),
                        School = text.School(child.School),
                        ClassLevel = child.ClassLevel,
                        BirthYear = child.BirthYear,
                        Position = position,
                        CreatedAt = now,
                        Status = ChildStatus.Active,
                    }
            ),
        ];

        // Recorded before the rows, so a crash after them still gets every Child its Workspace (D33).
        var workspaces = await Task.WhenAll(
            added.Select(child =>
            {
                var workspace = new CreateWorkspacePayload(family.FamilyId, child.ChildId, child.ClassLevel);
                return actions.RecordAsync(ActionType.CreateWorkspace, workspace.ActionId(), workspace, cancellationToken);
            })
        );

        await Families.RegisterAsync(identity.Uid, family, parent, added, cancellationToken);

        await Task.WhenAll(workspaces.Select(message => actions.SendAsync(message, cancellationToken)));

        BaseLog.FamilyRegistered(logger, family.FamilyId, added.Length);

        family.Parent = parent;
        family.Children = added;

        return family;
    }

    public async Task<Family> MeAsync(CancellationToken cancellationToken)
    {
        var parent =
            await Families.FindParentAsync(Identity.Uid, cancellationToken) ?? throw new NotRegisteredException();
        var family =
            await Families.SingleOrNullAsync(parent.FamilyId, parent.FamilyId, cancellationToken)
            ?? throw new InvalidOperationException("A parent's profile points at a family that has no family row.");

        family.Parent = parent;
        family.Children =
        [
            .. (await children.CollectionAsync(parent.FamilyId, cancellationToken)).Where(child =>
                child.Status == ChildStatus.Active
            ),
        ];

        return family;
    }

    // The ledger row is the commit point; removing the profiles locks every Parent out without a status check on
    // each request.
    public async Task EraseAsync(CancellationToken cancellationToken)
    {
        var familyId = await FamilyIdAsync(cancellationToken);
        var payload = new RemoveFamilyPayload(familyId);
        var message = await actions.RecordAsync(ActionType.RemoveFamily, familyId.ToString("N"), payload, cancellationToken);
        await Families.RemoveParentsAsync(familyId, cancellationToken);
        await actions.SendAsync(message, cancellationToken);
        BaseLog.FamilyDeleteRequested(logger, familyId);
    }
}
