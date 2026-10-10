namespace Lantern.Repository.Entities;

// families table, partition {familyId}, row "child_{childId:N}".
internal sealed class ChildEntity : TableEntityBase
{
    public Guid ChildId { get; set; }

    // Locked on the phone (D66); stored as sent.
    public string Name { get; set; }

    public string School { get; set; }

    public int ClassLevel { get; set; }

    public string BirthYear { get; set; }

    public int Position { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string Status { get; set; }
}
