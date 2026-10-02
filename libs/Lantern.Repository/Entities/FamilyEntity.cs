namespace Lantern.Repository.Entities;

// families table, partition {familyId}, row "family".
internal sealed class FamilyEntity : TableEntityBase
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; } = string.Empty;

    // Unwrap to decrypt the Family's fields; clearing it makes them unreadable forever (crypto-shredding).
    public string WrappedFieldKey { get; set; } = string.Empty;

    public string KeyScheme { get; set; } = "KeyVault";

    public DateTimeOffset CreatedAt { get; set; }

    // Touched under the row's ETag by every add, so two concurrent adds conflict instead of both passing the limit.
    public DateTimeOffset? ChildrenChangedAt { get; set; }
}
