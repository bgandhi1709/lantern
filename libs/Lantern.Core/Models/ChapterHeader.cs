using Lantern.Core.Constants;

namespace Lantern.Core.Models;

public sealed class ChapterHeader
{
    public ChapterHeader(ChapterPlace place, ChapterProgress progress)
    {
        ArgumentNullException.ThrowIfNull(place);

        ChapterId = place.Chapter.ChapterId;
        Number = place.Number;
        Title = place.Chapter.Title;
        SubjectId = place.SubjectId;
        SubjectName = place.SubjectName;
        State = progress?.State ?? ChapterState.NotStarted;
        AttemptCount = progress?.AttemptCount ?? 0;
        CheckCount = progress?.CheckCount ?? 0;
        ConceptsDone = progress?.ConceptsDone;
        ConceptCount = progress?.ConceptCount;
    }

    public string ChapterId { get; }

    public int Number { get; }

    public string Title { get; }

    public string SubjectId { get; }

    public string SubjectName { get; }

    public ChapterState State { get; }

    public int AttemptCount { get; }

    public int CheckCount { get; }

    public int? ConceptsDone { get; }

    public int? ConceptCount { get; }
}
