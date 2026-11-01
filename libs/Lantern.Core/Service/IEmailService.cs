namespace Lantern.Core.Service;

public interface IEmailService
{
    Task SendAsync(string recipient, string subject, string text, CancellationToken cancellationToken);
}
