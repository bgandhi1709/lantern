namespace Lantern.Core.Models;

public sealed class SubjectChapters(Page<ChapterEntry> page, SubjectProgress progress)
{
    public IReadOnlyList<ChapterEntry> Items { get; } = page.Items;

    public string NextCursor { get; } = page.NextCursor;

    public SubjectProgress Progress { get; } = progress;
}
