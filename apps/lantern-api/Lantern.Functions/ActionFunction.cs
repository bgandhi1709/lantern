using System.Text.Json;
using Lantern.Core.Actions;
using Lantern.Core.Repository;
using Lantern.Core.Service;

namespace Lantern.Functions;

public abstract class ActionFunction<TPayload>(IActionLedger ledger, IJsonService json)
{
    protected async Task ExecuteAsync(string body, CancellationToken cancellationToken)
    {
        var message = json.Deserialize<ActionMessage>(body) ?? throw new JsonException("The message body is not an action.");

        if (!await ledger.IsPendingAsync(message.Type, message.Id, cancellationToken))
        {
            return;
        }

        var payload =
            json.Deserialize<TPayload>(message.Payload)
            ?? throw new JsonException($"The {message.Type} payload is empty.");

        await HandleAsync(payload, cancellationToken);
        await ledger.CompleteAsync(message.Type, message.Id, cancellationToken);
    }

    protected abstract Task HandleAsync(TPayload payload, CancellationToken cancellationToken);
}
