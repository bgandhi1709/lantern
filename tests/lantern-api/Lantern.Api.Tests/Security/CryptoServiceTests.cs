using System.Security.Cryptography;
using Lantern.Core.Configuration;
using Lantern.Core.Security;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Tests.Security;

public sealed class CryptoServiceTests
{
    private static CryptoService New(string securityKey) => new(Options.Create(new SecurityOptions { Key = securityKey }));

    private static string RandomKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Hash_IsStableCaseSensitiveAndNotTheUid()
    {
        var crypto = New(RandomKey());

        Assert.Equal(crypto.Hash("uid-1"), crypto.Hash("uid-1"));
        Assert.NotEqual(crypto.Hash("uid-a"), crypto.Hash("uid-A"));
        Assert.DoesNotContain("uid-1", crypto.Hash("uid-1"), StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_WithAnotherSecurityKey_GivesAnotherHash() =>
        Assert.NotEqual(New(RandomKey()).Hash("uid-1"), New(RandomKey()).Hash("uid-1"));

    [Fact]
    public void Constructing_WithNoSettings_TouchesNothing_SoAHostThatNeverHashesNeedsNone() => _ = New(string.Empty);
}
