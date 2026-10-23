using Lantern.Core.Constants;

namespace Lantern.Core.Models;

public sealed class Attempt
{
    public int AttemptNumber { get; set; }

    public DateTimeOffset PlannedAt { get; set; }

    public int ConceptCount { get; set; }

    public int ConceptsDone { get; set; }

    public IReadOnlyList<Check> Checks { get; set; } = [];

    public ChapterState State => ChapterStates.Of(Checks.Count, ConceptsDone, ConceptCount);
}
