using Lantern.Core.Constants;

namespace Lantern.Api.Models;

public sealed class AttemptModel
{
    public int AttemptNumber { get; set; }

    public DateTimeOffset PlannedAt { get; set; }

    public ChapterState State { get; set; }

    public int ConceptsDone { get; set; }

    public int ConceptCount { get; set; }

    public IReadOnlyList<CheckModel> Checks { get; set; } = [];
}
