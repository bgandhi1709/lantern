namespace Lantern.Core.Models;

public sealed class Subject : IEquatable<Subject>
{
    public Subject(string subjectId, string name, string icon, int chapterCount)
    {
        SubjectId = subjectId;
        Name = name;
        Icon = icon;
        ChapterCount = chapterCount;
    }

    public string SubjectId { get; }

    public string Name { get; }

    public string Icon { get; }

    public int ChapterCount { get; }

    public bool Equals(Subject other) =>
        other is not null && SubjectId == other.SubjectId && Name == other.Name && Icon == other.Icon && ChapterCount == other.ChapterCount;

    public override bool Equals(object obj) => Equals(obj as Subject);

    public override int GetHashCode() => HashCode.Combine(SubjectId, Name, Icon, ChapterCount);
}
