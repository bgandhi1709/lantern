using System.Security.Cryptography;
using Lantern.Api.Test.Integration.Host;

namespace Lantern.Api.Test.Integration.Local;

public sealed class LocalKeyVaultClientTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("lantern-kek-").FullName;

    private string KeyPath => Path.Combine(directory, "kek.pem");

    [Fact]
    public async Task WrapThenUnwrap_ReturnsTheSameKey()
    {
        using var client = new LocalKeyVaultClient(KeyPath);
        var key = RandomNumberGenerator.GetBytes(32);

        var wrapped = await client.WrapKeyAsync(key, CancellationToken.None);

        Assert.NotEqual(key, wrapped);
        Assert.Equal(key, await client.UnwrapKeyAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public async Task Unwrap_AfterRestart_WorksBecauseTheKeyIsReadFromTheFile()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        byte[] wrapped;
        using (var first = new LocalKeyVaultClient(KeyPath))
        {
            wrapped = await first.WrapKeyAsync(key, CancellationToken.None);
        }

        using var restarted = new LocalKeyVaultClient(KeyPath);

        Assert.Equal(key, await restarted.UnwrapKeyAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public async Task Unwrap_WithAnotherKey_Throws()
    {
        using var client = new LocalKeyVaultClient(KeyPath);
        using var other = new LocalKeyVaultClient(Path.Combine(directory, "other.pem"));
        var wrapped = await client.WrapKeyAsync(new byte[32], CancellationToken.None);

        await Assert.ThrowsAnyAsync<CryptographicException>(() => other.UnwrapKeyAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public async Task Unwrap_TamperedWrappedKey_Throws()
    {
        using var client = new LocalKeyVaultClient(KeyPath);
        var wrapped = await client.WrapKeyAsync(new byte[32], CancellationToken.None);
        wrapped[^1] ^= 0xFF;

        await Assert.ThrowsAnyAsync<CryptographicException>(() => client.UnwrapKeyAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public async Task Wrap_SameKeyTwice_GivesDifferentWrappedBytes()
    {
        using var client = new LocalKeyVaultClient(KeyPath);
        var key = new byte[32];

        Assert.NotEqual(await client.WrapKeyAsync(key, CancellationToken.None), await client.WrapKeyAsync(key, CancellationToken.None));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
