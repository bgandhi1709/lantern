namespace Lantern.Repository.Entities;

// families table, partition {familyId}, row "family".
internal sealed class FamilyEntity : TableEntityBase
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; } = string.Empty;

    // Missing on a row written before Board existed: read as CBSE.
    public string? Board { get; set; }

    // The Family key wrapped on the phone, once by the Passphrase and once by the Recovery code (D66). Lantern cannot
    // open them; clearing them makes the locked fields unreadable forever (crypto-shredding).
    public string PassphraseWrappedKey { get; set; } = string.Empty;

    public string PassphraseSalt { get; set; } = string.Empty;

    public string RecoveryWrappedKey { get; set; } = string.Empty;

    public string RecoverySalt { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Touched under the row's ETag by every add, so two concurrent adds conflict instead of both passing the limit.
    public DateTimeOffset? ChildrenChangedAt { get; set; }
}
