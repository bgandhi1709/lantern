using Lantern.Core.Security;
using Lantern.Repository.Entities;
using Lantern.Repository.UnitOfWork;

namespace Lantern.Repository.Security;

internal sealed class FamilyKeyRing(IUnitOfWork<FamilyEntity> families, IRowKeyService keyService, ICryptoService crypto)
    : IFamilyKeyRing
{
    private readonly Dictionary<Guid, byte[]> unwrapped = [];

    public async Task<byte[]> GetAsync(Guid familyId, CancellationToken cancellationToken)
    {
        if (unwrapped.TryGetValue(familyId, out var key))
        {
            return key;
        }

        var family =
            await families.SingleOrNullAsync(keyService.FamilyPartition(familyId), keyService.FamilyRowKey, cancellationToken)
            ?? throw new InvalidOperationException("A Family has no family row.");

        return unwrapped[familyId] = await crypto.UnwrapKeyAsync(family.WrappedFieldKey, cancellationToken);
    }

    public async Task<(byte[] Key, string WrappedKey)> CreateAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var created = await crypto.GenerateKeyAsync(cancellationToken);
        unwrapped[familyId] = created.Key;

        return created;
    }
}
