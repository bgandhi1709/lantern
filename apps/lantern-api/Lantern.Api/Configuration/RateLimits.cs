using System.Threading.RateLimiting;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Configuration;

internal static class RateLimits
{
    public const string Children = "children";

    private const string CodePath = "/v1/security/code";
    private const string VerifyPath = "/v1/security/code/verify";

    public static void AddCodeLimits(this RateLimiterOptions options)
    {
        options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
            PerParent(IsCodeRequest, TimeSpan.FromMinutes(1), settings => settings.CodeRequestsPerMinute),
            PerParent(IsCodeRequest, TimeSpan.FromHours(1), settings => settings.CodeRequestsPerHour),
            PerParent(IsCodeCheck, TimeSpan.FromMinutes(1), settings => settings.CodeChecksPerMinute)
        );
        options.OnRejected = (context, _) =>
        {
            var http = context.HttpContext;
            http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (IsCodeCheck(http))
            {
                throw new LanternException(LanternErrorCode.CodeAttemptsUsed);
            }

            if (IsCodeRequest(http))
            {
                throw new LanternException(LanternErrorCode.CodeTooSoon);
            }

            return ValueTask.CompletedTask;
        };
    }

    private static bool IsCodeRequest(HttpContext context) =>
        HttpMethods.IsPost(context.Request.Method)
        && context.Request.Path.StartsWithSegments(CodePath, StringComparison.OrdinalIgnoreCase)
        && !IsCodeCheck(context);

    private static bool IsCodeCheck(HttpContext context) =>
        HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments(VerifyPath, StringComparison.OrdinalIgnoreCase);

    private static PartitionedRateLimiter<HttpContext> PerParent(
        Func<HttpContext, bool> applies,
        TimeSpan window,
        Func<RateLimitOptions, int> limit
    ) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!applies(context))
            {
                return RateLimitPartition.GetNoLimiter("unlimited");
            }

            var uid = context.User.FindFirst("sub")?.Value;
            var key = uid is null ? "anonymous" : context.RequestServices.GetRequiredService<ICryptoService>().Hash(uid);

            return RateLimitPartition.GetFixedWindowLimiter(
                key,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit(context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value),
                    Window = window,
                }
            );
        });

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
