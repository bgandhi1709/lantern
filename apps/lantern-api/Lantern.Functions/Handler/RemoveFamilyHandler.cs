using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Microsoft.Extensions.Logging;

namespace Lantern.Functions.Handler;

// Erases a Family whose Parents the API already unregistered. The rows go before the Workspaces, so a Workspace being
// created at the same moment either sees its Child gone and removes itself or is caught by the sweep.
internal sealed class RemoveFamilyHandler(
    IActionLedger ledger,
    IWorkspaceStore workspaces,
    IFamilyRepository families,
    ILogger<RemoveFamilyHandler> logger
) : ActionHandler<RemoveFamilyPayload>(ledger)
{
    public override ActionType Type => ActionType.RemoveFamily;

    protected override async Task HandleAsync(RemoveFamilyPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await families.EraseAsync(payload.FamilyId, cancellationToken);
        await workspaces.RemoveFamilyAsync(payload.FamilyId, cancellationToken);

        FunctionLog.FamilyErased(logger, payload.FamilyId);
    }
}
