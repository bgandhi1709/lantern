using System.Collections.Concurrent;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Service;

namespace Lantern.Api.Tests.Infrastructure;

public sealed class RecordingEmailService : IEmailService
{
    private readonly ConcurrentQueue<SentEmail> sent = new();

    public IReadOnlyList<SentEmail> Sent => [.. sent];

    public bool Down { get; set; }

    public Task SendAsync(string recipient, string subject, string text, CancellationToken cancellationToken)
    {
        if (Down)
        {
            throw new LanternException(LanternErrorCode.EmailNotSent);
        }

        sent.Enqueue(new SentEmail(recipient, subject, text));

        return Task.CompletedTask;
    }
}

public sealed class SentEmail(string to, string subject, string text)
{
    public string To { get; } = to;

    public string Subject { get; } = subject;

    public string Text { get; } = text;
}
