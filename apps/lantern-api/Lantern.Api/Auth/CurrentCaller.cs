using System.Security.Claims;
using Lantern.Api.Exceptions;

namespace Lantern.Api.Auth;

internal sealed class CurrentCaller(IHttpContextAccessor accessor) : ICurrentCaller
{
    public Caller Require()
    {
        var principal = accessor.HttpContext?.User ?? throw new CallerNotIdentifiedException();
        var uid = principal.FindFirstValue("sub");

        return string.IsNullOrWhiteSpace(uid)
            ? throw new CallerNotIdentifiedException()
            : new Caller(
                uid,
                principal.FindFirstValue("name") ?? string.Empty,
                principal.FindFirstValue("email") ?? string.Empty
            );
    }
}
