using System.Security.Cryptography;
using Lantern.Api.Services.Interfaces;

namespace Lantern.Api.Tests.Infrastructure;

// Key Vault has no local emulator. This stands in for it: same wrap algorithm (RSA-OAEP-256), but
// the private key lives in test process memory instead of a vault, so tests never touch the network.
public sealed class LocalRsaFamilyKeyWrapper : IFamilyKeyWrapper
{
    private readonly RSA rsa = RSA.Create(2048);

    public Task<byte[]> WrapAsync(byte[] dek, CancellationToken cancellationToken) =>
        Task.FromResult(this.rsa.Encrypt(dek, RSAEncryptionPadding.OaepSHA256));

    public Task<byte[]> UnwrapAsync(byte[] wrapped, CancellationToken cancellationToken) =>
        Task.FromResult(this.rsa.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA256));
}
