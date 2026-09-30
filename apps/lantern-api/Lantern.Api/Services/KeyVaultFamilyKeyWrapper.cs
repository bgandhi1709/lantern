using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Lantern.Api.Services.Interfaces;

namespace Lantern.Api.Services;

// The only place a family's field-encryption key ever gets wrapped or unwrapped. The key-encrypting
// key itself (family-field-key in Key Vault) never leaves the vault: WrapKeyAsync/UnwrapKeyAsync are
// remote calls, not local crypto.
internal sealed class KeyVaultFamilyKeyWrapper : IFamilyKeyWrapper
{
    private readonly CryptographyClient cryptoClient;

    public KeyVaultFamilyKeyWrapper(KeyClient keyClient, string keyName) =>
        cryptoClient = keyClient.GetCryptographyClient(keyName);

    public async Task<byte[]> WrapAsync(byte[] dek, CancellationToken cancellationToken)
    {
        var result = await cryptoClient.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, dek, cancellationToken);

        return result.EncryptedKey;
    }

    public async Task<byte[]> UnwrapAsync(byte[] wrapped, CancellationToken cancellationToken)
    {
        var result = await cryptoClient.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, wrapped, cancellationToken);

        return result.Key;
    }
}
