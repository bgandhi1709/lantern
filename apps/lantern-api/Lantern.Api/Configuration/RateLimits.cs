using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Lantern.Core.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Configuration;

internal static class RateLimits
{
    public const string Children = "children";

    private const string CodePath = "/v1/security/code";
    private const string VerifyPath = "/v1/security/code/verify";
    private const string TooSoonCode = "code-too-soon";
    private const string AttemptsUsedCode = "code-attempts-used";

    public static void AddCodeLimits(this RateLimiterOptions options)
    {
        options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
            PerParent(IsCodeRequest, TimeSpan.FromMinutes(1), settings => settings.CodeRequestsPerMinute),
            PerParent(IsCodeRequest, TimeSpan.FromHours(1), settings => settings.CodeRequestsPerHour),
            PerParent(IsCodeCheck, TimeSpan.FromMinutes(1), settings => settings.CodeChecksPerMinute)
        );
        options.OnRejected = async (context, cancellationToken) =>
        {
            var http = context.HttpContext;
            http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            var code = IsCodeCheck(http) ? AttemptsUsedCode : IsCodeRequest(http) ? TooSoonCode : null;
            if (code is null)
            {
                return;
            }

            http.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many tries",
                Detail = "Wait a little before trying again.",
                Instance = http.Request.Path,
            };
            problem.Extensions["code"] = code;
            await http.Response.WriteAsJsonAsync(problem, cancellationToken);
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
