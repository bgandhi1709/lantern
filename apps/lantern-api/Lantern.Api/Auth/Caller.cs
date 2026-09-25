using System.Security.Claims;

namespace Lantern.Api.Auth;

public sealed record Caller(string Uid, string Name, string Email)
{
    public static Caller? From(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var uid = principal.FindFirstValue("sub");

        return string.IsNullOrWhiteSpace(uid)
            ? null
            : new Caller(
                uid,
                principal.FindFirstValue("name") ?? string.Empty,
                principal.FindFirstValue("email") ?? string.Empty
            );
    }
}
