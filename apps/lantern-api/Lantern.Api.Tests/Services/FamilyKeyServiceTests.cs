using Lantern.Api.Services;
using Lantern.Api.Tests.Infrastructure;

namespace Lantern.Api.Tests.Services;

public sealed class FamilyKeyServiceTests
{
    private readonly FamilyKeyService keys = new(new LocalRsaFamilyKeyWrapper());

    [Fact]
    public async Task GenerateThenUnwrap_ReturnsTheSameDek()
    {
        var (dek, wrapped) = await keys.GenerateAsync(CancellationToken.None);

        Assert.Equal(dek, await keys.UnwrapAsync(wrapped, CancellationToken.None));
    }

    [Fact]
    public async Task Generate_TwiceForDifferentFamilies_GivesDifferentDeksAndDifferentWrappedKeys()
    {
        var first = await keys.GenerateAsync(CancellationToken.None);
        var second = await keys.GenerateAsync(CancellationToken.None);

        Assert.NotEqual(first.Dek, second.Dek);
        Assert.NotEqual(first.WrappedFieldKey, second.WrappedFieldKey);
    }

    [Fact]
    public async Task Unwrap_TamperedWrappedKey_Throws()
    {
        var (_, wrapped) = await keys.GenerateAsync(CancellationToken.None);
        var raw = Convert.FromBase64String(wrapped);
        raw[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(raw);

        await Assert.ThrowsAnyAsync<Exception>(() => keys.UnwrapAsync(tampered, CancellationToken.None));
    }

    [Fact]
    public async Task Unwrap_GarbageWrappedKey_Throws() =>
        await Assert.ThrowsAnyAsync<Exception>(() => keys.UnwrapAsync(Convert.ToBase64String([1, 2, 3]), CancellationToken.None));
}
