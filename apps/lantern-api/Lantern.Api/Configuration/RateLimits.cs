using System.Threading.RateLimiting;
using Lantern.Api.Services.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace Lantern.Api.Configuration;

internal static class RateLimits
{
    public const string Children = "children";
    private const int PermitsPerMinute = 30;

    // Keyed by the hashed uid, never the raw uid or the IP. In memory per replica: a cap, not an exact count.
    public static void AddChildrenPolicy(this RateLimiterOptions options) =>
        options.AddPolicy(
            Children,
            context =>
            {
                var uid = context.User.FindFirst("sub")?.Value;
                var key = uid is null ? "anonymous" : context.RequestServices.GetRequiredService<IUidHasher>().Hash(uid);

                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = PermitsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                    }
                );
            }
        );
}
