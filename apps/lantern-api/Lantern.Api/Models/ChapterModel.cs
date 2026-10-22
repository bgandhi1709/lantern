using Lantern.Core.Constants;

namespace Lantern.Api.Models;

public sealed class ChapterModel
{
    public string ChapterId { get; set; }

    public int Number { get; set; }

    public string Title { get; set; }

    public string Summary { get; set; }

    public ChapterState State { get; set; }

    public int AttemptCount { get; set; }

    public int? ConceptsDone { get; set; }

    public int? ConceptCount { get; set; }
}
