namespace Lantern.Core.Models;

public sealed class ChapterAttempts(ChapterHeader chapter, Page<Attempt> page)
{
    public ChapterHeader Chapter { get; } = chapter;

    public IReadOnlyList<Attempt> Items { get; } = page.Items;

    public string NextCursor { get; } = page.NextCursor;
}
