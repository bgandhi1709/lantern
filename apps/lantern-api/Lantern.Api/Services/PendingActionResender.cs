using Lantern.Api.Logging;
using Lantern.Core.Actions;
using Lantern.Core.Configuration;
using Lantern.Core.Repository;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Services;

// Runs once when the API starts, not on a timer: it sends again any action whose message may have been lost between
// the ledger row and the queue. The handlers are safe to repeat, so a message that was not lost does no harm.
internal sealed class PendingActionResender(
    IActionLedger ledger,
    IActionPublisher publisher,
    IOptions<ActionOptions> options,
    TimeProvider clock,
    ILogger<PendingActionResender> logger
) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunOnceAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The ledger itself failed (for example, the table is unreachable): the next start tries again.
            Log.ActionResendFailed(logger, ex.GetType().Name);
        }
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var message in await ledger.UnsentSinceAsync(clock.GetUtcNow() - options.Value.ResendAfter, cancellationToken))
        {
            await publisher.SendAsync(message, cancellationToken);
        }
    }
}
