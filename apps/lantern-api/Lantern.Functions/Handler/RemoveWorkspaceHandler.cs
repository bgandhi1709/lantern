using Lantern.Core.Actions;
using Lantern.Core.Repository;
using Microsoft.Extensions.Logging;

namespace Lantern.Functions.Handler;

// Removes a Child's Workspace (every Class in it) and the Child row. Every step is safe to repeat, so a crash anywhere
// resumes cleanly. Anything that later stores data under a Child adds its cleanup here (ADR-0003).
internal sealed class RemoveWorkspaceHandler(
    IActionLedger ledger,
    IWorkspaceStore workspaces,
    IChildRepository children,
    ILogger<RemoveWorkspaceHandler> logger
) : ActionHandler<RemoveWorkspacePayload>(ledger)
{
    public override ActionType Type => ActionType.RemoveWorkspace;

    protected override async Task HandleAsync(RemoveWorkspacePayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await workspaces.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);
        await children.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);
        // Again after the row is gone, for a Workspace created while the first sweep ran.
        await workspaces.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);

        FunctionLog.WorkspaceRemoved(logger, payload.FamilyId, payload.ChildId);
    }
}
