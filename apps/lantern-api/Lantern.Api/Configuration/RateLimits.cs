using System.Threading.RateLimiting;
using Lantern.Core.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Configuration;

internal static class RateLimits
{
    public const string Children = "children";

    // Keyed by the hashed uid, never the raw uid or the IP. In memory per replica: a cap, not an exact count.
    public static void AddChildrenPolicy(this RateLimiterOptions options) =>
        options.AddPolicy(
            Children,
            context =>
            {
                var uid = context.User.FindFirst("sub")?.Value;
                var key = uid is null ? "anonymous" : context.RequestServices.GetRequiredService<ICryptoService>().Hash(uid);

                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value.ChildrenPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                    }
                );
            }
        );
}
