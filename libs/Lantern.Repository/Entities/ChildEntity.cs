namespace Lantern.Repository.Entities;

// families table, partition {familyId}, row "child_{childId:N}".
internal sealed class ChildEntity : TableEntityBase
{
    public Guid ChildId { get; set; }

    // Locked on the phone (D66); stored as sent.
    public string NameLocked { get; set; } = string.Empty;

    public string? SchoolLocked { get; set; }

    public int ClassLevel { get; set; }

    public string BirthYearLocked { get; set; } = string.Empty;

    public int Position { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string Status { get; set; } = string.Empty;
}
