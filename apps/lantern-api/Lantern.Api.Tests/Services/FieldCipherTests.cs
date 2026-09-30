using System.Security.Cryptography;
using Lantern.Api.Configuration;
using Lantern.Api.Services;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Tests.Services;

public sealed class FieldCipherTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    private readonly FieldCipher cipher = new();

    [Fact]
    public void Unprotect_RoundTripsWhatProtectWrote()
    {
        var stored = this.cipher.Protect(Key, "Aarav", "p1", "child_1", "name");

        Assert.Equal("Aarav", this.cipher.Unprotect(Key, stored, "p1", "child_1", "name"));
    }

    [Fact]
    public void Protect_SamePlaintextTwice_GivesDifferentCiphertext() =>
        Assert.NotEqual(
            this.cipher.Protect(Key, "Aarav", "p1", "child_1", "name"),
            this.cipher.Protect(Key, "Aarav", "p1", "child_1", "name")
        );

    [Fact]
    public void Protect_Output_DoesNotContainThePlaintext() =>
        Assert.DoesNotContain("Aarav", this.cipher.Protect(Key, "Aarav", "p1", "child_1", "name"), StringComparison.Ordinal);

    [Theory]
    [InlineData("other-partition", "child_1", "name")]
    [InlineData("p1", "child_2", "name")]
    [InlineData("p1", "child_1", "school")]
    public void Unprotect_ValueMovedToAnotherRowOrColumn_Throws(string partition, string row, string column)
    {
        var stored = this.cipher.Protect(Key, "Aarav", "p1", "child_1", "name");

        Assert.ThrowsAny<CryptographicException>(() => this.cipher.Unprotect(Key, stored, partition, row, column));
    }

    [Fact]
    public void Unprotect_TamperedValue_Throws()
    {
        var stored = this.cipher.Protect(Key, "Aarav", "p1", "child_1", "name");
        var raw = Convert.FromBase64String(stored[3..]);
        raw[^1] ^= 0xFF;
        var tampered = "v1." + Convert.ToBase64String(raw);

        Assert.ThrowsAny<CryptographicException>(() => this.cipher.Unprotect(Key, tampered, "p1", "child_1", "name"));
    }

    [Fact]
    public void Unprotect_ValueFromAnotherKey_Throws()
    {
        var otherKey = RandomNumberGenerator.GetBytes(32);
        var stored = this.cipher.Protect(otherKey, "Aarav", "p1", "child_1", "name");

        Assert.ThrowsAny<CryptographicException>(() => this.cipher.Unprotect(Key, stored, "p1", "child_1", "name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v2.AAAA")]
    [InlineData("v1.not-base64!")]
    [InlineData("v1.AAAA")]
    public void Unprotect_MalformedValue_ThrowsCryptographicException(string value) =>
        Assert.ThrowsAny<CryptographicException>(() => this.cipher.Unprotect(Key, value, "p1", "child_1", "name"));

    [Fact]
    public void Hash_IsStableCaseSensitiveAndNotTheUid()
    {
        var securityKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var keys = NewHasher(securityKey);

        Assert.Equal(keys.Hash("uid-1"), keys.Hash("uid-1"));
        Assert.NotEqual(keys.Hash("uid-a"), keys.Hash("uid-A"));
        Assert.DoesNotContain("uid-1", keys.Hash("uid-1"), StringComparison.Ordinal);
        Assert.NotEqual(keys.Hash("uid-1"), NewHasher(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))).Hash("uid-1"));
    }

    private static KeyMaterial NewHasher(string securityKey) => new(Options.Create(new SecurityOptions { Key = securityKey }));
}
