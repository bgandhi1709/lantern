namespace Lantern.Api.Models;

public sealed class ChaptersPageModel
{
    public IReadOnlyList<ChapterModel> Items { get; set; } = [];

    public string NextCursor { get; set; }

    public SubjectProgressModel Progress { get; set; }
}
