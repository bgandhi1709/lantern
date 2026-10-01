using Lantern.Api.Configuration;
using Lantern.Api.Logging;
using Lantern.Api.Repository;
using Lantern.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Services;

// Finishes the deletes the API recorded. Every step is safe to repeat, so a crash anywhere resumes cleanly, and the
// pending row goes last. Anything that later stores data under a Child adds its cleanup here (ADR-0003).
internal sealed class ChildDeletionWorker(
    IChildDeletionStore deletions,
    IClassSpaceStore classSpaces,
    TimeProvider clock,
    IOptions<ChildDeletionOptions> options,
    ILogger<ChildDeletionWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollSeconds), clock);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The store itself failed (for example, the table is unreachable): the next tick tries again.
                Log.ChildDeleteSweepFailed(logger, ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        while (await deletions.ClaimNextAsync(clock.GetUtcNow(), cancellationToken) is { } job)
        {
            try
            {
                await classSpaces.DeleteChildAsync(job.FamilyId, job.ChildId, cancellationToken);
                await deletions.RemoveChildRowAsync(job, cancellationToken);
                // Again after the row is gone, for a Class space started while the first sweep ran.
                await classSpaces.DeleteChildAsync(job.FamilyId, job.ChildId, cancellationToken);
                await deletions.RemovePendingAsync(job, cancellationToken);

                Log.ChildDeleted(logger, job.FamilyId, job.ChildId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.ChildDeleteFailed(logger, job.FamilyId, job.ChildId, job.Attempts, ex.GetType().Name);
            }
        }
    }
}
