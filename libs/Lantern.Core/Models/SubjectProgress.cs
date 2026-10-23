using Lantern.Core.Constants;

namespace Lantern.Core.Models;

public sealed class SubjectProgress(IReadOnlyList<ChapterEntry> chapters)
{
    public IReadOnlyList<ChapterState> States { get; } = [.. chapters.Select(chapter => chapter.State)];

    public int NotStarted => Count(ChapterState.NotStarted);

    public int Planned => Count(ChapterState.Planned);

    public int Taught => Count(ChapterState.Taught);

    public int Checked => Count(ChapterState.Checked);

    private int Count(ChapterState state) => States.Count(candidate => candidate == state);
}
