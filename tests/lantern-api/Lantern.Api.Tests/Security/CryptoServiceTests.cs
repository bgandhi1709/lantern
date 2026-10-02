using System.Security.Cryptography;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Configuration;
using Lantern.Core.Security;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Tests.Security;

public sealed class CryptoServiceTests : IDisposable
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    private readonly LocalRsaKeyVaultClient keyVault = new();

    private CryptoService Crypto => New(keyVault, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    // ---- field cipher ----

    [Fact]
    public void Unprotect_RoundTripsWhatProtectWrote()
    {
        var stored = Crypto.Protect(Key, "Aarav", "p1", "child_1", "name");

        Assert.Equal("Aarav", Crypto.Unprotect(Key, stored, "p1", "child_1", "name"));
    }

    [Fact]
    public void Protect_SamePlaintextTwice_GivesDifferentCiphertext() =>
        Assert.NotEqual(Crypto.Protect(Key, "Aarav", "p1", "child_1", "name"), Crypto.Protect(Key, "Aarav", "p1", "child_1", "name"));

    [Fact]
    public void Protect_Output_DoesNotContainThePlaintext() =>
        Assert.DoesNotContain("Aarav", Crypto.Protect(Key, "Aarav", "p1", "child_1", "name"), StringComparison.Ordinal);

    [Theory]
    [InlineData("other-partition", "child_1", "name")]
    [InlineData("p1", "child_2", "name")]
    [InlineData("p1", "child_1", "school")]
    public void Unprotect_ValueMovedToAnotherRowOrColumn_Throws(string partition, string row, string column)
    {
        var stored = Crypto.Protect(Key, "Aarav", "p1", "child_1", "name");

        Assert.ThrowsAny<CryptographicException>(() => Crypto.Unprotect(Key, stored, partition, row, column));
    }

    [Fact]
    public void Unprotect_TamperedValue_Throws()
    {
        var stored = Crypto.Protect(Key, "Aarav", "p1", "child_1", "name");
        var raw = Convert.FromBase64String(stored[3..]);
        raw[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => Crypto.Unprotect(Key, "v1." + Convert.ToBase64String(raw), "p1", "child_1", "name"));
    }

    [Fact]
    public void Unprotect_ValueFromAnotherFamilyKey_Throws()
    {
        var stored = Crypto.Protect(RandomNumberGenerator.GetBytes(32), "Aarav", "p1", "child_1", "name");

        Assert.ThrowsAny<CryptographicException>(() => Crypto.Unprotect(Key, stored, "p1", "child_1", "name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v2.AAAA")]
    [InlineData("v1.not-base64!")]
    [InlineData("v1.AAAA")]
    public void Unprotect_MalformedValue_ThrowsCryptographicException(string value) =>
        Assert.ThrowsAny<CryptographicException>(() => Crypto.Unprotect(Key, value, "p1", "child_1", "name"));

    // ---- uid hash ----

    [Fact]
    public void Hash_IsStableCaseSensitiveAndNotTheUid()
    {
        var crypto = Crypto;

        Assert.Equal(crypto.Hash("uid-1"), crypto.Hash("uid-1"));
        Assert.NotEqual(crypto.Hash("uid-a"), crypto.Hash("uid-A"));
        Assert.DoesNotContain("uid-1", crypto.Hash("uid-1"), StringComparison.Ordinal);
        Assert.NotEqual(crypto.Hash("uid-1"), Crypto.Hash("uid-1"));
    }

    // ---- Family key wrapping ----

    [Fact]
    public async Task GenerateThenUnwrap_ReturnsTheSameKey()
    {
        var (key, wrapped) = await Crypto.GenerateKeyAsync(CancellationToken.None);

        Assert.Equal(key, await Crypto.UnwrapKeyAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public async Task Generate_TwiceForDifferentFamilies_GivesDifferentKeysAndDifferentWrappedKeys()
    {
        var first = await Crypto.GenerateKeyAsync(CancellationToken.None);
        var second = await Crypto.GenerateKeyAsync(CancellationToken.None);

        Assert.NotEqual(first.Key, second.Key);
        Assert.NotEqual(first.WrappedKey, second.WrappedKey);
    }

    [Fact]
    public async Task Unwrap_TamperedWrappedKey_Throws()
    {
        var (_, wrapped) = await Crypto.GenerateKeyAsync(CancellationToken.None);
        var raw = Convert.FromBase64String(wrapped);
        raw[^1] ^= 0xFF;

        await Assert.ThrowsAnyAsync<CryptographicException>(() => Crypto.UnwrapKeyAsync(Convert.ToBase64String(raw), CancellationToken.None));
    }

    [Fact]
    public async Task Unwrap_KeyWrappedByAnotherVault_Throws()
    {
        using var otherVault = new LocalRsaKeyVaultClient();
        var (_, wrapped) = await New(otherVault, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))).GenerateKeyAsync(CancellationToken.None);

        await Assert.ThrowsAnyAsync<CryptographicException>(() => Crypto.UnwrapKeyAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public void Constructing_WithNoSettings_TouchesNothing_SoAHostThatNeverHashesNeedsNone()
    {
        var crypto = New(keyVault, string.Empty);

        Assert.Equal("Aarav", crypto.Unprotect(Key, crypto.Protect(Key, "Aarav", "p1", "child_1", "name"), "p1", "child_1", "name"));
    }

    private static CryptoService New(IKeyVaultClient keyVault, string securityKey) =>
        new(keyVault, Options.Create(new SecurityOptions { Key = securityKey }));

    public void Dispose() => keyVault.Dispose();
}
