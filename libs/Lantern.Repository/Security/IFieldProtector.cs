using Azure.Data.Tables;

namespace Lantern.Repository.Security;

/// <summary>Encrypts and decrypts, in place, every <see cref="Entities.EncryptedAttribute"/> column of an entity.</summary>
internal interface IFieldProtector
{
    bool HasEncryptedColumns<TEntity>()
        where TEntity : ITableEntity;

    /// <remarks>Set the entity's partition and row keys first: they are the authenticated data.</remarks>
    void Protect<TEntity>(TEntity entity, byte[] familyKey)
        where TEntity : ITableEntity;

    void Unprotect<TEntity>(TEntity entity, byte[] familyKey)
        where TEntity : ITableEntity;
}
