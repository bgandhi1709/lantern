namespace Lantern.Core.Security;

public interface ICryptoService
{
    /// <summary>A new Family key, and the same key wrapped by Key Vault for storage.</summary>
    Task<(byte[] Key, string WrappedKey)> GenerateKeyAsync(CancellationToken cancellationToken);

    Task<byte[]> UnwrapKeyAsync(string wrappedKey, CancellationToken cancellationToken);

    /// <summary>AES-GCM with the row and column as authenticated data, so a value copied elsewhere fails to decrypt.</summary>
    string Protect(byte[] familyKey, string plaintext, string partitionKey, string rowKey, string column);

    /// <exception cref="System.Security.Cryptography.CryptographicException">Tampered, wrong key, or wrong row or column.</exception>
    string Unprotect(byte[] familyKey, string value, string partitionKey, string rowKey, string column);

    /// <summary>The keyed, deterministic hash of a uid, used to find a Parent before any Family key is known.</summary>
    string Hash(string uid);
}
