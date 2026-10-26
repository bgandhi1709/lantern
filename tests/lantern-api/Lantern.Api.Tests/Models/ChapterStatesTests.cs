using Lantern.Core.Constants;

namespace Lantern.Api.Tests.Models;

public sealed class ChapterStatesTests
{
    [Theory]
    [InlineData(0, 0, 5, ChapterState.Planned)]
    [InlineData(0, 4, 5, ChapterState.Planned)]
    [InlineData(0, 5, 5, ChapterState.Taught)]
    [InlineData(0, 0, 0, ChapterState.Planned)]
    [InlineData(1, 0, 5, ChapterState.Checked)]
    [InlineData(3, 5, 5, ChapterState.Checked)]
    public void Of_IsDerivedFromTheLatestAttemptsFacts(int checks, int conceptsDone, int conceptCount, ChapterState expected) =>
        Assert.Equal(expected, ChapterStates.Of(checks, conceptsDone, conceptCount));
}
