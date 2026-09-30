using System.Security.Cryptography;
using Lantern.Api.Services.Interfaces;

namespace Lantern.Api.Services;

internal sealed class FamilyKeyService(IFamilyKeyWrapper wrapper) : IFamilyKeyService
{
    private const int DekBytes = 32;

    public async Task<(byte[] Dek, string WrappedFieldKey)> GenerateAsync(CancellationToken cancellationToken)
    {
        var dek = RandomNumberGenerator.GetBytes(DekBytes);
        var wrapped = await wrapper.WrapAsync(dek, cancellationToken);

        return (dek, Convert.ToBase64String(wrapped));
    }

    public async Task<byte[]> UnwrapAsync(string wrappedFieldKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wrappedFieldKey);

        var wrapped = Convert.FromBase64String(wrappedFieldKey);

        return await wrapper.UnwrapAsync(wrapped, cancellationToken);
    }
}
