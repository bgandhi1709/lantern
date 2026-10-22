namespace Lantern.Api.Models;

public sealed class ChapterHistoryModel
{
    public ChapterHeaderModel Chapter { get; set; }

    public IReadOnlyList<AttemptModel> Items { get; set; } = [];

    public string NextCursor { get; set; }
}
