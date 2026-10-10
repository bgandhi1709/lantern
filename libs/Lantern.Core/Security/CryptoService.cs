using System.Security.Cryptography;
using System.Text;
using Lantern.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Lantern.Core.Security;

// Lantern holds no Family key (D66): personal fields are locked on the phone. Only the uid hash is computed here.
internal sealed class CryptoService(IOptions<SecurityOptions> options) : ICryptoService
{
    private const int KeyBytes = 32;

    // Derived on first use, so a host that never hashes (the Functions app) needs no Security key.
    private readonly Lazy<byte[]> uidKey = new(() => Derive(options.Value.Key, "lantern-uid-hash"));

    public string Hash(string uid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);

        return Convert.ToHexStringLower(HMACSHA256.HashData(uidKey.Value, Encoding.UTF8.GetBytes(uid)));
    }

    private static byte[] Derive(string key, string purpose) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Convert.FromBase64String(key),
            outputLength: KeyBytes,
            salt: [],
            info: Encoding.UTF8.GetBytes(purpose)
        );
}
