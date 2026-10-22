namespace Lantern.Repository.Entities;

// families table, partition {familyId}, row "family".
internal sealed class FamilyEntity : TableEntityBase
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; }

    // Missing on a row written before Board existed: read as CBSE.
    public string Board { get; set; }

    // The Family key wrapped on the phone by the Recovery code (ADR-0015). Lantern cannot open it; clearing it makes
    // the locked fields unreadable forever (crypto-shredding).
    public string RecoveryWrappedKey { get; set; }

    public string RecoverySalt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Touched under the row's ETag by every add, so two concurrent adds conflict instead of both passing the limit.
    public DateTimeOffset? ChildrenChangedAt { get; set; }
}
