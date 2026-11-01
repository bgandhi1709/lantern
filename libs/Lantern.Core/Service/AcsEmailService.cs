using Azure;
using Azure.Communication.Email;
using Lantern.Core.Configuration;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lantern.Core.Service;

internal sealed class AcsEmailService(IOptions<EmailOptions> options, ILogger<AcsEmailService> logger) : IEmailService
{
    private readonly Lazy<EmailClient> client = new(() =>
        new EmailClient(options.Value.ConnectionString, new EmailClientOptions { Retry = { MaxRetries = 0 } })
    );

    public async Task SendAsync(string recipient, string subject, string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);

        var message = new EmailMessage(options.Value.Sender, recipient, new EmailContent(subject) { PlainText = text });

        try
        {
            await client.Value.SendAsync(WaitUntil.Started, message, cancellationToken);
        }
        catch (RequestFailedException failure)
        {
            CoreLog.EmailNotSent(logger, failure.Status);

            throw new LanternException(LanternErrorCode.EmailNotSent);
        }
    }
}
