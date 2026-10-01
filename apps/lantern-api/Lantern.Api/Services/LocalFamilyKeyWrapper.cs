using System.Security.Cryptography;
using Lantern.Api.Services.Interfaces;

namespace Lantern.Api.Services;

// Development only: stands in for Key Vault (same RSA-OAEP-256) with a key in a PEM file, created on first use so the
// API and the seeder share it through a volume. Program refuses to register this outside Development.
internal sealed class LocalFamilyKeyWrapper : IFamilyKeyWrapper
{
    private readonly RSA rsa = RSA.Create(2048);

    public LocalFamilyKeyWrapper(string keyPath)
    {
        if (File.Exists(keyPath))
        {
            rsa.ImportFromPem(File.ReadAllText(keyPath));
            return;
        }

        File.WriteAllText(keyPath, rsa.ExportRSAPrivateKeyPem());
    }

    public Task<byte[]> WrapAsync(byte[] dek, CancellationToken cancellationToken) =>
        Task.FromResult(rsa.Encrypt(dek, RSAEncryptionPadding.OaepSHA256));

    public Task<byte[]> UnwrapAsync(byte[] wrapped, CancellationToken cancellationToken) =>
        Task.FromResult(rsa.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA256));
}
