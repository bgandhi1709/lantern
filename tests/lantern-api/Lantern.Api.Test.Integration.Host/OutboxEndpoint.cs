using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Api.Test.Integration.Host;

internal sealed class OutboxEndpoint : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Map(
                "/dev/outbox",
                branch => branch.Run(async context =>
                {
                    var recipient = context.Request.Query["to"].ToString();
                    var emails = context.RequestServices
                        .GetRequiredService<OutboxEmailService>()
                        .Sent.Where(email => recipient.Length == 0 || email.Recipient == recipient);

                    await context.Response.WriteAsJsonAsync(emails, context.RequestAborted);
                })
            );
            next(app);
        };
    }
}
