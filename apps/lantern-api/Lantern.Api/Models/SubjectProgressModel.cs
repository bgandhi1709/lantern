using Lantern.Core.Constants;

namespace Lantern.Api.Models;

public sealed class SubjectProgressModel
{
    public int NotStarted { get; set; }

    public int Planned { get; set; }

    public int Taught { get; set; }

    public int Checked { get; set; }

    public IReadOnlyList<ChapterState> States { get; set; } = [];
}
