using System.Buffers.Text;
using System.Collections.Concurrent;
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

    private readonly ConcurrentDictionary<string, byte[]> purposeKeys = new();

    public string Sign(string purpose, string data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentNullException.ThrowIfNull(data);

        return Base64Url.EncodeToString(HMACSHA256.HashData(KeyFor(purpose), Encoding.UTF8.GetBytes(data)));
    }

    public bool Matches(string purpose, string data, string signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Sign(purpose, data)),
            Encoding.UTF8.GetBytes(signature)
        );
    }

    private byte[] KeyFor(string purpose) =>
        purposeKeys.GetOrAdd(purpose, name => Derive(options.Value.Key, $"lantern-sign-{name}"));

    private static byte[] Derive(string key, string purpose) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Convert.FromBase64String(key),
            outputLength: KeyBytes,
            salt: [],
            info: Encoding.UTF8.GetBytes(purpose)
        );
}
