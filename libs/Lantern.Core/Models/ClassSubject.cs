namespace Lantern.Core.Models;

public sealed class ClassSubject
{
    public string SubjectId { get; set; }

    public string Name { get; set; }

    public string Icon { get; set; }

    public int ChapterCount { get; set; }

    public List<Chapter> Chapters { get; set; } = [];
}
