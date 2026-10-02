using System.Text.Json;
using Lantern.Core.Configuration;
using Lantern.Core.Logging;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lantern.Core.Actions;

internal sealed class ActionPublisher(
    IServiceBusService serviceBus,
    IActionLedger ledger,
    IOptions<ActionOptions> options,
    TimeProvider clock,
    ILogger<ActionPublisher> logger
) : IActionPublisher
{
    public async Task<ActionMessage> RecordAsync<TPayload>(
        ActionType type,
        string id,
        TPayload payload,
        CancellationToken cancellationToken
    )
    {
        var message = new ActionMessage(id, type, JsonSerializer.Serialize(payload, JsonSerializerOptions.Web));

        await ledger.RecordAsync(message, clock.GetUtcNow(), cancellationToken);

        return message;
    }

    public async Task SendAsync(ActionMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            await serviceBus.SendAsync(options.Value.Queue, message, cancellationToken);
            await ledger.MarkSentAsync(message.Type, message.Id, clock.GetUtcNow(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CoreLog.ActionPublishFailed(logger, message.Type, ex.GetType().Name);
        }
    }
}
