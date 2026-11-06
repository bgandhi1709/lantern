using Lantern.Core.Actions;
using Lantern.Core.Configuration;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lantern.Functions;

// Erases a Family whose Parents the API already unregistered. The rows go before the Workspaces, so a Workspace being
// created at the same moment either sees its Child gone and removes itself or is caught by the sweep.
internal sealed class RemoveFamilyFunction(
    IActionLedger ledger,
    IJsonService json,
    IOptions<ActionQueueOptions> options,
    IWorkspaceStore workspaces,
    IProgressRepository progress,
    IFamilyRepository families,
    ILogger<RemoveFamilyFunction> logger
) : ActionFunction<RemoveFamilyPayload>(ledger, json, options)
{
    [Function("RemoveFamily")]
    [RabbitMQOutput(QueueName = "%Actions:ParkingQueue%", ConnectionStringSetting = "Broker:Url")]
    public Task<string[]> RunAsync(
        [RabbitMQTrigger("%Actions:RemoveFamilyQueue%", ConnectionStringSetting = "Broker:Url")] string myQueueItem,
        FunctionContext context,
        CancellationToken cancellationToken
    ) => ExecuteAsync(myQueueItem, context, cancellationToken);

    protected override async Task HandleAsync(RemoveFamilyPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await families.EraseAsync(payload.FamilyId, cancellationToken);
        await workspaces.RemoveFamilyAsync(payload.FamilyId, cancellationToken);
        await progress.RemoveFamilyAsync(payload.FamilyId, cancellationToken);

        FunctionLog.FamilyErased(logger, payload.FamilyId);
    }
}
