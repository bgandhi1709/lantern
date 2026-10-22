using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Api.Test.Integration.Host;

internal sealed class SeedProgressEndpoint : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Map(
                "/dev/seed-progress",
                branch => branch.Run(async context =>
                {
                    if (!HttpMethods.IsPost(context.Request.Method))
                    {
                        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;

                        return;
                    }

                    Guid? onlyChild = Guid.TryParse(context.Request.Query["childId"], out var childId) ? childId : null;
                    var seeded = await context.RequestServices.GetRequiredService<SampleProgress>().SeedAsync(onlyChild, context.RequestAborted);
                    await context.Response.WriteAsJsonAsync(new { seeded }, context.RequestAborted);
                })
            );
            next(app);
        };
    }
}
