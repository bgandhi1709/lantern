using System.Reflection;
using Azure.Data.Tables;
using Lantern.Core.Security;
using Lantern.Repository.Entities;

namespace Lantern.Repository.Security;

internal sealed class FieldProtector(ICryptoService crypto) : IFieldProtector
{
    public bool HasEncryptedColumns<TEntity>()
        where TEntity : ITableEntity => Columns<TEntity>.All.Length > 0;

    public void Protect<TEntity>(TEntity entity, byte[] familyKey)
        where TEntity : ITableEntity
    {
        ArgumentNullException.ThrowIfNull(entity);

        foreach (var (property, column) in Columns<TEntity>.All)
        {
            if (property.GetValue(entity) is string plaintext)
            {
                property.SetValue(entity, crypto.Protect(familyKey, plaintext, entity.PartitionKey, entity.RowKey, column));
            }
        }
    }

    public void Unprotect<TEntity>(TEntity entity, byte[] familyKey)
        where TEntity : ITableEntity
    {
        ArgumentNullException.ThrowIfNull(entity);

        foreach (var (property, column) in Columns<TEntity>.All)
        {
            if (property.GetValue(entity) is string cipher)
            {
                property.SetValue(entity, crypto.Unprotect(familyKey, cipher, entity.PartitionKey, entity.RowKey, column));
            }
        }
    }

    private static class Columns<TEntity>
    {
        public static readonly (PropertyInfo Property, string Column)[] All =
        [
            .. typeof(TEntity)
                .GetProperties()
                .Select(property => (property, property.GetCustomAttribute<EncryptedAttribute>()?.Column))
                .Where(pair => pair.Column is not null)
                .Select(pair => (pair.property, pair.Column!)),
        ];
    }
}
