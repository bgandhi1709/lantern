namespace Lantern.Repository.Entities;

// parents table, partition {uidHash}, row "profile". Written last at registration: it is the commit point.
internal sealed class ParentEntity : TableEntityBase
{
    public Guid ParentId { get; set; }

    public Guid FamilyId { get; set; }

    [Encrypted("name")]
    public string NameCipher { get; set; } = string.Empty;

    [Encrypted("email")]
    public string EmailCipher { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string ConsentVersion { get; set; } = string.Empty;

    public DateTimeOffset ConsentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
