using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Lantern.Core.Actions;
using Lantern.Functions.Handler;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Lantern.Functions;

// The one trigger for every action type: it hands the message to the dispatcher, which picks the handler by type.
// Messages are settled by hand. A failed one is left unsettled on purpose, so its lock expiring is the retry delay and
// the queue's delivery limit moves a message that never succeeds to the dead-letter queue.
public sealed class ActionFunction(IActionDispatcher dispatcher, ILogger<ActionFunction> logger)
{
    [Function("DispatchAction")]
    public async Task RunAsync(
        [ServiceBusTrigger("%Actions:Queue%", Connection = "ServiceBus", AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(actions);

        ActionMessage action;
        try
        {
            action = message.Body.ToObjectFromJson<ActionMessage>(JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            action = null;
        }

        if (action is null)
        {
            await RejectAsync(message, actions, "The message body is not an action.", cancellationToken);
            return;
        }

        try
        {
            await dispatcher.DispatchAsync(action, cancellationToken);
            await actions.CompleteMessageAsync(message, cancellationToken);
        }
        catch (UnknownActionException)
        {
            await RejectAsync(message, actions, $"No handler for action type '{action.Type}'.", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            FunctionLog.ActionFailed(logger, action.Type, action.Id, message.DeliveryCount, ex.GetType().Name);
        }
    }

    private async Task RejectAsync(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        string reason,
        CancellationToken cancellationToken
    )
    {
        FunctionLog.ActionRejected(logger, message.MessageId, reason);

        await actions.DeadLetterMessageAsync(
            message,
            deadLetterReason: "Rejected",
            deadLetterErrorDescription: reason,
            cancellationToken: cancellationToken
        );
    }
}
