using System.Security.Cryptography;
using Lantern.Core.Security;

namespace Lantern.Api.Tests.Infrastructure;

// Key Vault has no local emulator. This stands in for it: same wrap algorithm (RSA-OAEP-256), but the private key
// lives in test process memory instead of a vault, so tests never touch the network and still run the real cipher.
public sealed class LocalRsaKeyVaultClient : IKeyVaultClient, IDisposable
{
    private readonly RSA rsa = RSA.Create(2048);

    public Task<byte[]> WrapKeyAsync(byte[] key, CancellationToken cancellationToken) =>
        Task.FromResult(rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256));

    public Task<byte[]> UnwrapKeyAsync(byte[] wrapped, CancellationToken cancellationToken) =>
        Task.FromResult(rsa.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA256));

    public void Dispose() => rsa.Dispose();
}
