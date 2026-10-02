namespace Lantern.Core.Security;

// The boundary to whatever holds the key-encrypting key, and the one piece local runs and tests replace. The
// production client never lets that key leave Key Vault: it only sends and receives wrapped bytes.
public interface IKeyVaultClient
{
    Task<byte[]> WrapKeyAsync(byte[] key, CancellationToken cancellationToken);

    Task<byte[]> UnwrapKeyAsync(byte[] wrapped, CancellationToken cancellationToken);
}
