using Lantern.Core.Configuration;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Extensions.Options;

namespace Lantern.Functions.Tests.Infrastructure;

internal sealed class RecordingPayload
{
    public string FamilyId { get; set; } = string.Empty;
}

internal class RecordingFunction(IActionLedger ledger, int retryLimit = 1000)
    : ActionFunction<RecordingPayload>(ledger, new JsonService(), Options.Create(new ActionQueueOptions { RetryLimit = retryLimit }))
{
    public int Handled { get; private set; }

    public string Name => Subscriber;

    public Task<string[]> RunAsync(string body, CancellationToken cancellationToken = default) => RunAsync(body, 0, cancellationToken);

    public Task<string[]> RunAsync(string body, int cycles, CancellationToken cancellationToken = default) =>
        ExecuteAsync(body, DeliveryContext.AfterCycles(cycles), cancellationToken);

    protected override Task HandleAsync(RecordingPayload payload, CancellationToken cancellationToken)
    {
        Handled++;
        return Task.CompletedTask;
    }
}
