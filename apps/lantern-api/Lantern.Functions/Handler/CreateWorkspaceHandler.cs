using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Microsoft.Extensions.Logging;

namespace Lantern.Functions.Handler;

// The ledger row is written before the Child row, so a message can name a Child whose add failed: that one does
// nothing. A removal that finished while the Workspace was being created would leave it orphaned, so it is swept again.
internal sealed class CreateWorkspaceHandler(
    IActionLedger ledger,
    IWorkspaceStore workspaces,
    IChildRepository children,
    ILogger<CreateWorkspaceHandler> logger
) : ActionHandler<CreateWorkspacePayload>(ledger)
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

        if (!await IsActiveAsync(payload, cancellationToken))
        {
            await workspaces.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);
            return;
        }

        FunctionLog.WorkspaceCreated(logger, payload.FamilyId, payload.ChildId, payload.ClassLevel);
    }

    private async Task<bool> IsActiveAsync(CreateWorkspacePayload payload, CancellationToken cancellationToken) =>
        await children.StatusAsync(payload.FamilyId, payload.ChildId, cancellationToken) == ChildStatus.Active;
}
