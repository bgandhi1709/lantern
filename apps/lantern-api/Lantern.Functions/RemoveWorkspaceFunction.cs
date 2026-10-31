using Lantern.Core.Actions;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Lantern.Functions;

internal sealed class RemoveWorkspaceFunction(
    IActionLedger ledger,
    IJsonService json,
    IWorkspaceStore workspaces,
    IProgressRepository progress,
    IChildRepository children,
    ILogger<RemoveWorkspaceFunction> logger
) : ActionFunction<RemoveWorkspacePayload>(ledger, json)
{
    [Function("RemoveWorkspace")]
    public Task RunAsync(
        [RabbitMQTrigger("%Actions:RemoveWorkspaceQueue%", ConnectionStringSetting = "Broker:Url")] string myQueueItem,
        CancellationToken cancellationToken
    ) => ExecuteAsync(myQueueItem, cancellationToken);

    protected override async Task HandleAsync(RemoveWorkspacePayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await workspaces.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);
        await progress.RemoveChildAsync(payload.FamilyId, payload.ChildId, cancellationToken);
        await children.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);

        FunctionLog.WorkspaceRemoved(logger, payload.FamilyId, payload.ChildId);
    }
}
