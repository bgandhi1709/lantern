using Lantern.Core.Repository;
using Lantern.Core.Service;

namespace Lantern.Functions.Tests.Infrastructure;

internal sealed class RecordingPayload
{
    public string FamilyId { get; set; } = string.Empty;
}

internal class RecordingFunction(IActionLedger ledger) : ActionFunction<RecordingPayload>(ledger, new JsonService())
{
    public int Handled { get; private set; }

    public string Name => Subscriber;

    public Task RunAsync(string body, CancellationToken cancellationToken = default) => ExecuteAsync(body, cancellationToken);

    protected override Task HandleAsync(RecordingPayload payload, CancellationToken cancellationToken)
    {
        Handled++;
        return Task.CompletedTask;
    }
}
