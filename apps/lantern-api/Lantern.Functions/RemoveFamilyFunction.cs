using System.Runtime.CompilerServices;
using Lantern.Core.Actions;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Lantern.Api.Tests")]
namespace Lantern.Functions;

// Erases a Family whose Parents the API already unregistered. The rows go before the Workspaces, so a Workspace being
// created at the same moment either sees its Child gone and removes itself or is caught by the sweep.
internal sealed class RemoveFamilyFunction(
    IActionLedger ledger,
    IJsonService json,
    IWorkspaceStore workspaces,
    IProgressRepository progress,
    IFamilyRepository families,
    ILogger<RemoveFamilyFunction> logger
) : ActionFunction<RemoveFamilyPayload>(ledger, json)
{
    [Function("RemoveFamily")]
    public Task RunAsync(
        [RabbitMQTrigger("%Actions:RemoveFamilyQueue%", ConnectionStringSetting = "Broker:Url")] string myQueueItem,
        CancellationToken cancellationToken
    ) => ExecuteAsync(myQueueItem, cancellationToken);

    protected override async Task HandleAsync(RemoveFamilyPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await families.EraseAsync(payload.FamilyId, cancellationToken);
        await workspaces.RemoveFamilyAsync(payload.FamilyId, cancellationToken);
        await progress.RemoveFamilyAsync(payload.FamilyId, cancellationToken);

        FunctionLog.FamilyErased(logger, payload.FamilyId);
    }
}
