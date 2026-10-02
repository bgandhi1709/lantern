namespace Lantern.Repository.Security;

/// <summary>The Family keys of one request: each is unwrapped by Key Vault at most once, and never kept past the request.</summary>
internal interface IFamilyKeyRing
{
    Task<byte[]> GetAsync(Guid familyId, CancellationToken cancellationToken);

    /// <summary>A new key for a new Family, and the same key wrapped by Key Vault for its family row.</summary>
    Task<(byte[] Key, string WrappedKey)> CreateAsync(Guid familyId, CancellationToken cancellationToken);
}
