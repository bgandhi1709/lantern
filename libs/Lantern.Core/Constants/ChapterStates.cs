namespace Lantern.Core.Constants;

public static class ChapterStates
{
    public static ChapterState Of(int checks, int conceptsDone, int conceptCount) =>
        checks > 0 ? ChapterState.Checked
        : conceptCount > 0 && conceptsDone >= conceptCount ? ChapterState.Taught
        : ChapterState.Planned;
}
