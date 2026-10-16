using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Extensions.Logging;

namespace Lantern.Functions.Handler;

// The ledger row is written before the Child row, so a message can name a Child whose add failed: that one does
// nothing. A removal that finished while the Workspace was being created would leave it orphaned, so it is swept again.
internal sealed class CreateWorkspaceHandler(
    IActionLedger ledger,
    IJsonService json,
    IWorkspaceStore workspaces,
    IChildRepository children,
    IFamilyRepository families,
    ILogger<CreateWorkspaceHandler> logger
) : ActionHandler<CreateWorkspacePayload>(ledger, json)
{
    public override ActionType Type => ActionType.CreateWorkspace;

    protected override async Task HandleAsync(CreateWorkspacePayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!await IsActiveAsync(payload, cancellationToken))
        {
            return;
        }

        await workspaces.CreateAsync(payload.FamilyId, payload.ChildId, payload.ClassLevel, cancellationToken);

        // The Family row is written in the same transaction as its Children, so an active Child always has one.
        var family =
            await families.SingleOrNullAsync(payload.FamilyId, payload.FamilyId, cancellationToken)
            ?? throw new InvalidOperationException("An active Child's Family has no row.");
        await workspaces.CopyContentAsync(payload.FamilyId, payload.ChildId, payload.ClassLevel, family.Board, cancellationToken);

        if (!await IsActiveAsync(payload, cancellationToken))
        {
            await workspaces.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);
            return;
        }

        await workspaces.AddToManifestAsync(payload.FamilyId, payload.ChildId, payload.ClassLevel, family.Board, cancellationToken);

        FunctionLog.WorkspaceCreated(logger, payload.FamilyId, payload.ChildId, payload.ClassLevel);
    }

    private async Task<bool> IsActiveAsync(CreateWorkspacePayload payload, CancellationToken cancellationToken) =>
        await children.StatusAsync(payload.FamilyId, payload.ChildId, cancellationToken) == ChildStatus.Active;
}
