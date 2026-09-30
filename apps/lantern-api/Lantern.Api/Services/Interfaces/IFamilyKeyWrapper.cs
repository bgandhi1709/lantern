namespace Lantern.Api.Services.Interfaces;

// The boundary to whatever actually holds the key-encrypting key. The production implementation
// never lets that key leave Key Vault: it only ever sends/receives wrapped bytes.
public interface IFamilyKeyWrapper
{
    Task<byte[]> WrapAsync(byte[] dek, CancellationToken cancellationToken);

    Task<byte[]> UnwrapAsync(byte[] wrapped, CancellationToken cancellationToken);
}
