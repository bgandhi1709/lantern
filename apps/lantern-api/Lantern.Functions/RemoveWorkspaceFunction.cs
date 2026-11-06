using Lantern.Core.Actions;
using Lantern.Core.Configuration;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lantern.Functions;

internal sealed class RemoveWorkspaceFunction(
    IActionLedger ledger,
    IJsonService json,
    IOptions<ActionQueueOptions> options,
    IWorkspaceStore workspaces,
    IProgressRepository progress,
    IChildRepository children,
    ILogger<RemoveWorkspaceFunction> logger
) : ActionFunction<RemoveWorkspacePayload>(ledger, json, options)
{
    [Function("RemoveWorkspace")]
    [RabbitMQOutput(QueueName = "%Actions:ParkingQueue%", ConnectionStringSetting = "Broker:Url")]
    public Task<string[]> RunAsync(
        [RabbitMQTrigger("%Actions:RemoveWorkspaceQueue%", ConnectionStringSetting = "Broker:Url")] string myQueueItem,
        FunctionContext context,
        CancellationToken cancellationToken
    ) => ExecuteAsync(myQueueItem, context, cancellationToken);

    protected override async Task HandleAsync(RemoveWorkspacePayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await workspaces.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);
        await progress.RemoveChildAsync(payload.FamilyId, payload.ChildId, cancellationToken);
        await children.RemoveAsync(payload.FamilyId, payload.ChildId, cancellationToken);

        FunctionLog.WorkspaceRemoved(logger, payload.FamilyId, payload.ChildId);
    }
}
