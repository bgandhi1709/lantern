using System.Security.Cryptography;
using Lantern.Core.Security;

namespace Lantern.Api.Test.Integration.Host;

// Local Docker only: stands in for Key Vault (same RSA-OAEP-256) with a key in a PEM file, created on first use.
internal sealed class LocalKeyVaultClient : IKeyVaultClient, IDisposable
{
    private readonly RSA rsa = RSA.Create(2048);

    public LocalKeyVaultClient(string keyPath)
    {
        if (File.Exists(keyPath))
        {
            rsa.ImportFromPem(File.ReadAllText(keyPath));
            return;
        }

        File.WriteAllText(keyPath, rsa.ExportRSAPrivateKeyPem());
    }

    public Task<byte[]> WrapKeyAsync(byte[] key, CancellationToken cancellationToken) =>
        Task.FromResult(rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256));

    public Task<byte[]> UnwrapKeyAsync(byte[] wrapped, CancellationToken cancellationToken) =>
        Task.FromResult(rsa.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA256));

    public void Dispose() => rsa.Dispose();
}
