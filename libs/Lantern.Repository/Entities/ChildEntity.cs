namespace Lantern.Repository.Entities;

// families table, partition {familyId}, row "child_{childId:N}".
internal sealed class ChildEntity : TableEntityBase
{
    public Guid ChildId { get; set; }

    [Encrypted("name")]
    public string NameCipher { get; set; } = string.Empty;

    [Encrypted("school")]
    public string? SchoolCipher { get; set; }

    public int ClassLevel { get; set; }

    public int BirthYear { get; set; }

    public int Position { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string Status { get; set; } = string.Empty;
}
