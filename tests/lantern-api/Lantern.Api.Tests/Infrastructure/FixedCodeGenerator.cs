using Lantern.Core.Service;

namespace Lantern.Api.Tests.Infrastructure;

public sealed class FixedCodeGenerator : IOneTimeCodeGenerator
{
    public const string Code = "123456";

    private int nonces;

    public string Current { get; set; } = Code;

    public string NewCode() => Current;

    public string NewNonce() => $"nonce-{Interlocked.Increment(ref nonces)}";
}
