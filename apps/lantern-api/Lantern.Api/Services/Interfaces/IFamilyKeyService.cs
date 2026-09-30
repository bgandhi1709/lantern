namespace Lantern.Api.Services.Interfaces;

public interface IFamilyKeyService
{
    Task<(byte[] Dek, string WrappedFieldKey)> GenerateAsync(CancellationToken cancellationToken);

    Task<byte[]> UnwrapAsync(string wrappedFieldKey, CancellationToken cancellationToken);
}
