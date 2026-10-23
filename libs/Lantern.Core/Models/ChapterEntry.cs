using Lantern.Core.Constants;

namespace Lantern.Core.Models;

public sealed class ChapterEntry
{
    public ChapterEntry(Chapter chapter, ChapterProgress progress)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        ChapterId = chapter.ChapterId;
        Number = chapter.Number;
        Title = chapter.Title;
        Summary = chapter.Summary;
        State = progress?.State ?? ChapterState.NotStarted;
        AttemptCount = progress?.AttemptCount ?? 0;
        ConceptsDone = progress?.ConceptsDone;
        ConceptCount = progress?.ConceptCount;
    }

    public string ChapterId { get; }

    public int Number { get; }

    public string Title { get; }

    public string Summary { get; }

    public ChapterState State { get; }

    public int AttemptCount { get; }

    public int? ConceptsDone { get; }

    public int? ConceptCount { get; }
}
