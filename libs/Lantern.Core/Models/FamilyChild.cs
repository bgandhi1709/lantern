namespace Lantern.Core.Models;

public sealed class FamilyChild
{
    public int Class { get; set; }

    public string ProfileVersion { get; set; }

    public List<Subject> Subjects { get; set; } = [];

    public bool Is(FamilyChild other) =>
        other is not null && Class == other.Class && ProfileVersion == other.ProfileVersion && Subjects.SequenceEqual(other.Subjects);
}
