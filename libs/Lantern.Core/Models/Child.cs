namespace Lantern.Core.Models;

public sealed class Child : IFamilyModel
{
    public const int MaxPerFamily = 6;

    public Guid FamilyId { get; set; }

    public Guid ChildId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? School { get; set; }

    public int ClassLevel { get; set; }

    public int BirthYear { get; set; }

    // Children saved together share a timestamp; Position keeps the order they were entered in.
    public int Position { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ChildStatus Status { get; set; }
}
