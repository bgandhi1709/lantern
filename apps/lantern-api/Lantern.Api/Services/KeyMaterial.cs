using System.Security.Cryptography;
using System.Text;
using Lantern.Api.Configuration;
using Lantern.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Services;

// Field-level encryption no longer derives from this: each family's field key is wrapped by
// Key Vault instead (KeyVaultFamilyKeyWrapper). This is only the deterministic uid-hash key,
// needed to find a row before any row-specific key exists.
internal sealed class KeyMaterial(IOptions<SecurityOptions> options) : IUidHasher
{
    private readonly byte[] uidKey = Derive(options.Value.Key, "lantern-uid-hash");

    public string Hash(string uid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);

        return Convert.ToHexStringLower(HMACSHA256.HashData(this.uidKey, Encoding.UTF8.GetBytes(uid)));
    }

    private static byte[] Derive(string key, string purpose) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Convert.FromBase64String(key),
            outputLength: 32,
            salt: [],
            info: Encoding.UTF8.GetBytes(purpose)
        );
}
