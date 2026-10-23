namespace Lantern.Core.Models;

public sealed class ChapterPlace(ClassSubject subject, Chapter chapter, int number)
{
    public string SubjectId { get; } = subject.SubjectId;

    public string SubjectName { get; } = subject.Name;

    public Chapter Chapter { get; } = chapter;

    public int Number { get; } = number;
}
