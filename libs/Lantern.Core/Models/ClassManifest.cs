namespace Lantern.Core.Models;

public sealed class ClassManifest : Manifest
{
    public string Board { get; set; }

    public string ProfileVersion { get; set; }

    public int Class { get; set; }

    public List<ClassSubject> Subjects { get; set; } = [];

    public ClassManifest EnsureChaptersMatchCounts()
    {
        var mismatch = Subjects.Find(subject => subject.Chapters.Count != subject.ChapterCount);
        if (mismatch is not null)
        {
            throw new InvalidOperationException($"The Class manifest says {mismatch.SubjectId} has {mismatch.ChapterCount} Chapters and lists {mismatch.Chapters.Count}.");
        }

        return this;
    }

    public IReadOnlyList<Chapter> ChaptersOf(string subjectId) => Subjects.Find(subject => subject.SubjectId == subjectId)?.Chapters;
}
