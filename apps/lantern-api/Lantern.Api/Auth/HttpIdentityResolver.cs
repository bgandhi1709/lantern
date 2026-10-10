using System.Security.Claims;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Identity;

namespace Lantern.Api.Auth;

// The caller comes only from the verified token: no route or body field names a Parent or a Family.
internal sealed class HttpIdentityResolver(IHttpContextAccessor accessor) : IIdentityResolver
{
    public CallerIdentity Identity
    {
        get
        {
            var principal = accessor.HttpContext?.User ?? throw new LanternException(LanternErrorCode.CallerNotIdentified);
            var uid = principal.FindFirstValue("sub");

            return string.IsNullOrWhiteSpace(uid)
                ? throw new LanternException(LanternErrorCode.CallerNotIdentified)
                : new CallerIdentity(
                    uid,
                    principal.FindFirstValue("name") ?? string.Empty,
                    principal.FindFirstValue("email") ?? string.Empty
                );
        }
    }
}
