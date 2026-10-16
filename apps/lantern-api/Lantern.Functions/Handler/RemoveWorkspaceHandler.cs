using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Extensions.Logging;

namespace Lantern.Functions.Handler;

internal sealed class RemoveWorkspaceHandler(
    IActionLedger ledger,
    IJsonService json,
    IWorkspaceStore workspaces,
    IChildRepository children,
    ILogger<RemoveWorkspaceHandler> logger
) : ActionHandler<RemoveWorkspacePayload>(ledger, json)
{
    public override ActionType Type => ActionType.RemoveWorkspace;

    protected override async Task HandleAsync(RemoveWorkspacePayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await workspaces.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);
        await children.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);

        FunctionLog.WorkspaceRemoved(logger, payload.FamilyId, payload.ChildId);
    }
}
