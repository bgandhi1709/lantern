using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Lantern.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Lantern.Core.Security;

// WrapKey and UnwrapKey are remote calls: the key-encrypting key (family-field-key) never leaves the vault. Built on
// first use, so a host that never wraps (the Functions app) needs no Key Vault settings.
internal sealed class KeyVaultClient(IOptions<KeyVaultOptions> options) : IKeyVaultClient
{
    private readonly Lazy<CryptographyClient> client = new(() =>
        new KeyClient(new Uri(options.Value.VaultUri), new DefaultAzureCredential()).GetCryptographyClient(
            options.Value.FamilyKeyName
        )
    );

    public async Task<byte[]> WrapKeyAsync(byte[] key, CancellationToken cancellationToken) =>
        (await client.Value.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, key, cancellationToken)).EncryptedKey;

    public async Task<byte[]> UnwrapKeyAsync(byte[] wrapped, CancellationToken cancellationToken) =>
        (await client.Value.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, wrapped, cancellationToken)).Key;
}
