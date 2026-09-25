using System.Security.Cryptography;
using System.Text;
using Lantern.Api.Services.Interfaces;

namespace Lantern.Api.Services;

internal sealed class FieldCipher(KeyMaterial keys) : IFieldCipher
{
    private const string Prefix = "v1.";
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    public string Protect(string plaintext, string partitionKey, string rowKey, string column)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var tag = new byte[TagBytes];
        var cipher = new byte[plain.Length];

        using var aes = new AesGcm(keys.FieldKey, TagBytes);
        aes.Encrypt(nonce, plain, cipher, tag, AssociatedData(partitionKey, rowKey, column));

        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(string value, string partitionKey, string rowKey, string column)
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

        using var aes = new AesGcm(keys.FieldKey, TagBytes);
        aes.Decrypt(nonce, cipher, tag, plain, AssociatedData(partitionKey, rowKey, column));

        return Encoding.UTF8.GetString(plain);
    }

    // Binds a value to its row and column, so a copied value fails to decrypt.
    private static byte[] AssociatedData(string partitionKey, string rowKey, string column) =>
        Encoding.UTF8.GetBytes($"{partitionKey}|{rowKey}|{column}");
}
