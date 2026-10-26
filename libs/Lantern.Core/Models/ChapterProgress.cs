using Lantern.Core.Constants;

namespace Lantern.Core.Models;

public sealed class ChapterProgress
{
    public string ChapterId { get; set; }

    public int AttemptCount { get; set; }

    public int LatestAttempt { get; set; }

    public int ConceptCount { get; set; }

    public int ConceptsDone { get; set; }

    public int LatestAttemptChecks { get; set; }

    public int CheckCount { get; set; }

    public DateTimeOffset? LatestCheckAt { get; set; }

    public int? LatestCheckRight { get; set; }

    public int? LatestCheckOf { get; set; }

    public DateTimeOffset LastActivityAt { get; set; }

    public ChapterState State => ChapterStates.Of(LatestAttemptChecks, ConceptsDone, ConceptCount);
}
