using System.Security.Cryptography;
using System.Text;
using Lantern.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Lantern.Core.Security;

internal sealed class CryptoService(IKeyVaultClient keyVault, IOptions<SecurityOptions> options) : ICryptoService
{
    private const int KeyBytes = 32;
    private const string Prefix = "v1.";
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    // Derived on first use, so a host that never hashes (the Functions app) needs no Security key.
    private readonly Lazy<byte[]> uidKey = new(() => Derive(options.Value.Key, "lantern-uid-hash"));

    public async Task<(byte[] Key, string WrappedKey)> GenerateKeyAsync(CancellationToken cancellationToken)
    {
        var key = RandomNumberGenerator.GetBytes(KeyBytes);
        var wrapped = await keyVault.WrapKeyAsync(key, cancellationToken);

        return (key, Convert.ToBase64String(wrapped));
    }

    public async Task<byte[]> UnwrapKeyAsync(string wrappedKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wrappedKey);

        return await keyVault.UnwrapKeyAsync(Convert.FromBase64String(wrappedKey), cancellationToken);
    }

    public string Protect(byte[] familyKey, string plaintext, string partitionKey, string rowKey, string column)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var tag = new byte[TagBytes];
        var cipher = new byte[plain.Length];

        using var aes = new AesGcm(familyKey, TagBytes);
        aes.Encrypt(nonce, plain, cipher, tag, AssociatedData(partitionKey, rowKey, column));

        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(byte[] familyKey, string value, string partitionKey, string rowKey, string column)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException("Unknown ciphertext version.");
        }

        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(value[Prefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Ciphertext is not valid base64.", ex);
        }

        if (raw.Length < NonceBytes + TagBytes)
        {
            throw new CryptographicException("Ciphertext is too short.");
        }

        var nonce = raw.AsSpan(0, NonceBytes);
        var tag = raw.AsSpan(NonceBytes, TagBytes);
        var cipher = raw.AsSpan(NonceBytes + TagBytes);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(familyKey, TagBytes);
        aes.Decrypt(nonce, cipher, tag, plain, AssociatedData(partitionKey, rowKey, column));

        return Encoding.UTF8.GetString(plain);
    }

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

    private static byte[] AssociatedData(string partitionKey, string rowKey, string column) =>
        Encoding.UTF8.GetBytes($"{partitionKey}|{rowKey}|{column}");
}
