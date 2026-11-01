using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Lantern.Core.Configuration;
using Lantern.Core.Service;
using Microsoft.Extensions.Options;

namespace Lantern.Core.Security;

internal sealed class CryptoService(IOptions<SecurityOptions> options, IJsonService json) : ICryptoService
{
    private const int KeyBytes = 32;
    private const char Separator = '.';

    private readonly Lazy<byte[]> uidKey = new(() => Derive(options.Value.Key, "lantern-uid-hash"));
    private readonly ConcurrentDictionary<string, byte[]> purposeKeys = new();

    public string Hash(string uid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);

        return Convert.ToHexStringLower(HMACSHA256.HashData(uidKey.Value, Encoding.UTF8.GetBytes(uid)));
    }

    public string Seal(string purpose, object payload, string binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(binding);

        var body = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json.Serialize(payload)));

        return $"{body}{Separator}{Signature(purpose, body, binding)}";
    }

    public T Open<T>(string purpose, string token, string binding)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentNullException.ThrowIfNull(binding);

        var parts = (token ?? string.Empty).Split(Separator);

        if (parts.Length != 2 || !Matches(Signature(purpose, parts[0], binding), parts[1]))
        {
            return null;
        }

        return json.Deserialize<T>(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[0])));
    }

    private string Signature(string purpose, string body, string binding) =>
        Base64Url.EncodeToString(
            HMACSHA256.HashData(
                purposeKeys.GetOrAdd(purpose, name => Derive(options.Value.Key, $"lantern-seal-{name}")),
                Encoding.UTF8.GetBytes($"{body}{Separator}{binding}")
            )
        );

    private static bool Matches(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    private static byte[] Derive(string key, string purpose) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Convert.FromBase64String(key),
            outputLength: KeyBytes,
            salt: [],
            info: Encoding.UTF8.GetBytes(purpose)
        );
}
