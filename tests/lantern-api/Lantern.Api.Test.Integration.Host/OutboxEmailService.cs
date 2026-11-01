using System.Collections.Concurrent;
using Lantern.Core.Service;

namespace Lantern.Api.Test.Integration.Host;

internal sealed class OutboxEmailService : IEmailService
{
    private readonly ConcurrentQueue<OutboxEmail> sent = new();

    public IReadOnlyList<OutboxEmail> Sent => [.. sent];

    public Task SendAsync(string recipient, string subject, string text, CancellationToken cancellationToken)
    {
        sent.Enqueue(new OutboxEmail(recipient, subject, text));

        return Task.CompletedTask;
    }
}

internal sealed class OutboxEmail(string recipient, string subject, string text)
{
    public string Recipient { get; } = recipient;

    public string Subject { get; } = subject;

    public string Text { get; } = text;
}
