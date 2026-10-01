using Lantern.Api.Services;

namespace Lantern.Api.Tests.Services;

public sealed class LocalFamilyKeyWrapperTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("lantern-kek-").FullName;

    private string KeyPath => Path.Combine(directory, "kek.pem");

    [Fact]
    public async Task WrapThenUnwrap_ReturnsTheSameKey()
    {
        var wrapper = new LocalFamilyKeyWrapper(KeyPath);
        var dek = new byte[32];
        Random.Shared.NextBytes(dek);

        var wrapped = await wrapper.WrapAsync(dek, CancellationToken.None);

        Assert.NotEqual(dek, wrapped);
        Assert.Equal(dek, await wrapper.UnwrapAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public async Task Unwrap_AfterRestart_WorksBecauseTheKeyIsReadFromTheFile()
    {
        var dek = new byte[32];
        Random.Shared.NextBytes(dek);
        var wrapped = await new LocalFamilyKeyWrapper(KeyPath).WrapAsync(dek, CancellationToken.None);

        var restarted = new LocalFamilyKeyWrapper(KeyPath);

        Assert.Equal(dek, await restarted.UnwrapAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public async Task Unwrap_WithAnotherKey_Throws()
    {
        var wrapped = await new LocalFamilyKeyWrapper(KeyPath).WrapAsync(new byte[32], CancellationToken.None);
        var other = new LocalFamilyKeyWrapper(Path.Combine(directory, "other.pem"));

        await Assert.ThrowsAnyAsync<Exception>(() => other.UnwrapAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public async Task Unwrap_TamperedWrappedKey_Throws()
    {
        var wrapper = new LocalFamilyKeyWrapper(KeyPath);
        var wrapped = await wrapper.WrapAsync(new byte[32], CancellationToken.None);
        wrapped[^1] ^= 0xFF;

        await Assert.ThrowsAnyAsync<Exception>(() => wrapper.UnwrapAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public async Task Wrap_SameKeyTwice_GivesDifferentWrappedBytes()
    {
        var wrapper = new LocalFamilyKeyWrapper(KeyPath);
        var dek = new byte[32];

        Assert.NotEqual(
            await wrapper.WrapAsync(dek, CancellationToken.None),
            await wrapper.WrapAsync(dek, CancellationToken.None)
        );
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
