using System.Text.Json;
using Lantern.Core.Actions;
using Lantern.Core.Repository;

namespace Lantern.Functions.Handler;

// Every handler gets the same guard: a message whose ledger row is gone (a repeat, or a redelivery after the action
// finished) does nothing, and the row is removed only after the handler's own steps succeed.
public abstract class ActionHandler<TPayload>(IActionLedger ledger) : IActionHandler
{
    public abstract ActionType Type { get; }

    public async Task HandleAsync(ActionMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!await ledger.IsPendingAsync(message.Type, message.Id, cancellationToken))
        {
            return;
        }

        var payload =
            JsonSerializer.Deserialize<TPayload>(message.Payload, JsonSerializerOptions.Web)
            ?? throw new JsonException($"The {message.Type} payload is empty.");

        await HandleAsync(payload, cancellationToken);
        await ledger.CompleteAsync(message.Type, message.Id, cancellationToken);
    }

    protected abstract Task HandleAsync(TPayload payload, CancellationToken cancellationToken);
}
