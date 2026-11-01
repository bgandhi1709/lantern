namespace Lantern.Core.Models;

public sealed class CodeToken
{
    public string UidHash { get; set; }

    public string Operation { get; set; }

    public string Target { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public string Nonce { get; set; }
}
