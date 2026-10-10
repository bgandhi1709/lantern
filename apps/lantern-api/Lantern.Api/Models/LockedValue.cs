namespace Lantern.Api.Models;

internal static class LockedValue
{
    // A locked 120-character value (the longest School) with its nonce, tag and base64 overhead, rounded up.
    public const int MaxLength = 400;
}
