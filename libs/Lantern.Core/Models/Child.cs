namespace Lantern.Core.Models;

public sealed class Child : IFamilyModel
{
    public const int MaxPerFamily = 6;

    // Balbharati Books exist for Classes 1 to 5 only (D-SSC); CBSE covers 1 to 10.
    public static bool IsClassAvailable(BoardType board, int classLevel) =>
        classLevel is >= 1 and <= 10 && (board == BoardType.Cbse || classLevel <= 5);

    public Guid FamilyId { get; set; }

    public Guid ChildId { get; set; }

    // Locked on the phone with the Family key (D66): the server stores and returns them as sent.
    public string NameLocked { get; set; } = string.Empty;

    public string? SchoolLocked { get; set; }

    public int ClassLevel { get; set; }

    public string BirthYearLocked { get; set; } = string.Empty;

    // Children saved together share a timestamp; Position keeps the order they were entered in.
    public int Position { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ChildStatus Status { get; set; }
}
