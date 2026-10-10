namespace Lantern.Repository.Entities;

// parents table, partition {uidHash}, row "profile". Written last at registration: it is the commit point.
internal sealed class ParentEntity : TableEntityBase
{
    public Guid ParentId { get; set; }

    public Guid FamilyId { get; set; }

    // Locked on the phone (D66); stored as sent.
    public string NameLocked { get; set; } = string.Empty;

    public string EmailLocked { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string ConsentVersion { get; set; } = string.Empty;

    public DateTimeOffset ConsentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
