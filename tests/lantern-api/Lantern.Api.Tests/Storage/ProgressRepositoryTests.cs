using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Repository;

namespace Lantern.Api.Tests.Storage;

[Collection(ApiCollection.Name)]
public sealed class ProgressRepositoryTests(AzuriteFixture azurite) : IDisposable
{
    private static readonly Guid FamilyId = Guid.NewGuid();
    private static readonly Guid ChildId = Guid.NewGuid();
    private static readonly DateTimeOffset Day = new(2026, 10, 20, 17, 0, 0, TimeSpan.Zero);

    private readonly RepositoryHarness harness = new(azurite.ConnectionString);

    private ProgressRepository Repository => harness.ProgressRepository();

    private static ChapterRef NumberPlay => new(FamilyId, ChildId, 6, "gegp103");

    [Fact]
    public async Task ChaptersAsync_AChildWhoStartedNothing_ReturnsNoRows() =>
        Assert.Empty(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));

    [Fact]
    public async Task StartAttemptAsync_TheFirstPlan_MakesTheChapterPlannedWithNoConceptDone()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);

        var chapter = Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));
        Assert.Equal("gegp103", chapter.ChapterId);
        Assert.Equal(ChapterState.Planned, chapter.State);
        Assert.Equal((1, 5, 0), (chapter.AttemptCount, chapter.ConceptCount, chapter.ConceptsDone));
        Assert.Equal(Day, chapter.LastActivityAt);
    }

    [Fact]
    public async Task StartAttemptAsync_TheSamePlanTwice_StartsOneAttempt()
    {
        var planId = Guid.NewGuid();

        await Repository.StartAttemptAsync(NumberPlay, planId, 5, "v1", Day, CancellationToken.None);
        await Repository.StartAttemptAsync(NumberPlay, planId, 5, "v1", Day.AddMinutes(1), CancellationToken.None);

        var chapter = Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));
        Assert.Equal(1, chapter.AttemptCount);
    }

    [Fact]
    public async Task StartAttemptAsync_ANewPlan_StartsASecondAttempt_AndTheCountRestarts()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 4, "v1", Day.AddDays(1), CancellationToken.None);

        var chapter = Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));
        Assert.Equal((2, 2, 4, 0), (chapter.AttemptCount, chapter.LatestAttempt, chapter.ConceptCount, chapter.ConceptsDone));
    }

    [Fact]
    public async Task MarkConceptDoneAsync_OneConcept_IsCounted_AndTheChapterStaysPlanned()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);

        await Repository.MarkConceptDoneAsync(NumberPlay, "c1", Day.AddHours(1), CancellationToken.None);

        var chapter = Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));
        Assert.Equal((ChapterState.Planned, 1), (chapter.State, chapter.ConceptsDone));
        Assert.Equal(Day.AddHours(1), chapter.LastActivityAt);
    }

    [Fact]
    public async Task MarkConceptDoneAsync_EveryConcept_MakesTheChapterTaught()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 2, "v1", Day, CancellationToken.None);

        await Repository.MarkConceptDoneAsync(NumberPlay, "c1", Day, CancellationToken.None);
        await Repository.MarkConceptDoneAsync(NumberPlay, "c2", Day, CancellationToken.None);

        Assert.Equal(ChapterState.Taught, Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None)).State);
    }

    [Fact]
    public async Task MarkConceptDoneAsync_TheSameConceptTwice_ChangesNothing()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);

        await Repository.MarkConceptDoneAsync(NumberPlay, "c1", Day.AddHours(1), CancellationToken.None);
        await Repository.MarkConceptDoneAsync(NumberPlay, "c1", Day.AddHours(2), CancellationToken.None);

        var chapter = Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));
        Assert.Equal((1, Day.AddHours(1)), (chapter.ConceptsDone, chapter.LastActivityAt));
    }

    [Fact]
    public async Task MarkConceptDoneAsync_AChapterWithNoAttempt_IsRefused() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repository.MarkConceptDoneAsync(NumberPlay, "c1", Day, CancellationToken.None));

    [Fact]
    public async Task MarkConceptDoneAsync_MoreConceptsThanThePlanHas_IsRefused()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 1, "v1", Day, CancellationToken.None);
        await Repository.MarkConceptDoneAsync(NumberPlay, "c1", Day, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Repository.MarkConceptDoneAsync(NumberPlay, "c2", Day, CancellationToken.None));
    }

    [Fact]
    public async Task MarkConceptDoneAsync_TwoPhonesAtOnce_BothConceptsCount()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);

        await Task.WhenAll(
            Enumerable.Range(1, 4).Select(n => harness.ProgressRepository().MarkConceptDoneAsync(NumberPlay, $"c{n}", Day.AddMinutes(n), CancellationToken.None))
        );

        Assert.Equal(4, Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None)).ConceptsDone);
    }

    [Fact]
    public async Task RecordCheckAsync_ACheck_MakesTheChapterChecked_WithItsLatestScore()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);

        await Repository.RecordCheckAsync(NumberPlay, Guid.NewGuid(), Day.AddDays(3), Marks(true, true, false, true, false), "v1", CancellationToken.None);

        var chapter = Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));
        Assert.Equal(ChapterState.Checked, chapter.State);
        Assert.Equal((1, 1, Day.AddDays(3), 3, 5), (chapter.CheckCount, chapter.LatestAttemptChecks, chapter.LatestCheckAt, chapter.LatestCheckRight, chapter.LatestCheckOf));
    }

    [Fact]
    public async Task RecordCheckAsync_TheSameCheckTwice_CountsOnce()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);
        var checkId = Guid.NewGuid();

        await Repository.RecordCheckAsync(NumberPlay, checkId, Day.AddDays(3), Marks(true, false), "v1", CancellationToken.None);
        await Repository.RecordCheckAsync(NumberPlay, checkId, Day.AddDays(4), Marks(true, true), "v1", CancellationToken.None);

        var chapter = Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));
        Assert.Equal((1, 1, 2), (chapter.CheckCount, chapter.LatestCheckRight, chapter.LatestCheckOf));
    }

    [Fact]
    public async Task RecordCheckAsync_ARetryInTheSameAttempt_IsAnotherCheck_AndTheLatestScoreIsTheRetry()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);

        await Repository.RecordCheckAsync(NumberPlay, Guid.NewGuid(), Day.AddDays(3), Marks(true, false, false), "v1", CancellationToken.None);
        await Repository.RecordCheckAsync(NumberPlay, Guid.NewGuid(), Day.AddDays(7), Marks(true, true, false), "v1", CancellationToken.None);

        var chapter = Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));
        Assert.Equal((2, 2, 2, 3), (chapter.CheckCount, chapter.LatestAttemptChecks, chapter.LatestCheckRight, chapter.LatestCheckOf));
    }

    [Fact]
    public async Task StartAttemptAsync_AfterACheck_MakesTheChapterPlannedAgain_AndKeepsTheEarlierScore()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);
        await Repository.RecordCheckAsync(NumberPlay, Guid.NewGuid(), Day.AddDays(3), Marks(true, false), "v1", CancellationToken.None);

        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day.AddDays(14), CancellationToken.None);

        var chapter = Assert.Single(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));
        Assert.Equal((ChapterState.Planned, 2, 0, 1), (chapter.State, chapter.AttemptCount, chapter.LatestAttemptChecks, chapter.CheckCount));
        Assert.Equal((1, 2), (chapter.LatestCheckRight, chapter.LatestCheckOf));
    }

    [Fact]
    public async Task RecordCheckAsync_AChapterWithNoAttempt_IsRefused() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repository.RecordCheckAsync(NumberPlay, Guid.NewGuid(), Day, Marks(true), "v1", CancellationToken.None));

    [Fact]
    public async Task RecordCheckAsync_ACheckWithNoQuestions_IsRefused()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() => Repository.RecordCheckAsync(NumberPlay, Guid.NewGuid(), Day, [], "v1", CancellationToken.None));
    }

    private static CheckedConcept[] Marks(params bool[] right) =>
        [.. right.Select((mark, index) => new CheckedConcept { ConceptId = $"c{index + 1}", Title = $"Concept {index + 1}", Right = mark })];

    [Fact]
    public async Task AttemptsAsync_AChapterNeverStarted_ReturnsNothing() =>
        Assert.Empty(await Repository.AttemptsAsync(NumberPlay, CancellationToken.None));

    [Fact]
    public async Task AttemptsAsync_TwoAttempts_ComeNewestFirst_EachWithItsChecksNewestFirst()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 3, "v1", Day, CancellationToken.None);
        await Repository.RecordCheckAsync(NumberPlay, Guid.NewGuid(), Day.AddDays(3), Marks(true, false, false), "v1", CancellationToken.None);
        await Repository.RecordCheckAsync(NumberPlay, Guid.NewGuid(), Day.AddDays(7), Marks(true, true, false), "v1", CancellationToken.None);
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 4, "v1", Day.AddDays(14), CancellationToken.None);
        await Repository.MarkConceptDoneAsync(NumberPlay, "c1", Day.AddDays(14), CancellationToken.None);

        var history = await Repository.AttemptsAsync(NumberPlay, CancellationToken.None);

        Assert.Equal([2, 1], history.Select(attempt => attempt.AttemptNumber));
        Assert.Equal([ChapterState.Planned, ChapterState.Checked], history.Select(attempt => attempt.State));
        Assert.Equal((4, 1, Day.AddDays(14)), (history[0].ConceptCount, history[0].ConceptsDone, history[0].PlannedAt));
        Assert.Empty(history[0].Checks);
        Assert.Equal([2, 1], history[1].Checks.Select(check => check.CheckNumber));
        Assert.Equal([(2, 3), (1, 3)], history[1].Checks.Select(check => (check.Right, check.Of)));
        Assert.Equal(Day.AddDays(7), history[1].Checks[0].TakenAt);
    }

    [Fact]
    public async Task AttemptsAsync_ACheck_KeepsEveryQuestionWithItsConceptAndMark()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 3, "v1", Day, CancellationToken.None);
        await Repository.RecordCheckAsync(NumberPlay, Guid.NewGuid(), Day, Marks(true, false), "v1", CancellationToken.None);

        var check = Assert.Single(Assert.Single(await Repository.AttemptsAsync(NumberPlay, CancellationToken.None)).Checks);

        Assert.Equal([("c1", "Concept 1", true), ("c2", "Concept 2", false)], check.Results.Select(result => (result.ConceptId, result.Title, result.Right)));
    }

    [Fact]
    public async Task AttemptsAsync_ReadsOnlyThatChapter_ThatChild_ThatFamily_AndThatClass()
    {
        var other = Guid.NewGuid();
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 3, "v1", Day, CancellationToken.None);
        await Repository.StartAttemptAsync(new ChapterRef(FamilyId, ChildId, 6, "gegp10"), Guid.NewGuid(), 3, "v1", Day, CancellationToken.None);
        await Repository.StartAttemptAsync(new ChapterRef(FamilyId, other, 6, "gegp103"), Guid.NewGuid(), 3, "v1", Day, CancellationToken.None);
        await Repository.StartAttemptAsync(new ChapterRef(other, ChildId, 6, "gegp103"), Guid.NewGuid(), 3, "v1", Day, CancellationToken.None);
        await Repository.StartAttemptAsync(new ChapterRef(FamilyId, ChildId, 5, "gegp103"), Guid.NewGuid(), 3, "v1", Day, CancellationToken.None);

        Assert.Single(await Repository.AttemptsAsync(NumberPlay, CancellationToken.None));
        Assert.Single(await Repository.AttemptsAsync(new ChapterRef(FamilyId, ChildId, 6, "gegp10"), CancellationToken.None));
        Assert.Equal(["gegp10", "gegp103"], (await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None)).Select(chapter => chapter.ChapterId).Order());
    }

    [Fact]
    public async Task ChapterAsync_AStartedChapter_ReturnsItsSummary_AndAnUnstartedOneNothing()
    {
        await Repository.StartAttemptAsync(NumberPlay, Guid.NewGuid(), 5, "v1", Day, CancellationToken.None);

        var started = await Repository.ChapterAsync(NumberPlay, CancellationToken.None);

        Assert.Equal((ChapterState.Planned, 1), (started.State, started.AttemptCount));
        Assert.Null(await Repository.ChapterAsync(new ChapterRef(FamilyId, ChildId, 6, "gegp104"), CancellationToken.None));
    }

    [Fact]
    public async Task RemoveChildAsync_MoreRowsThanOneTransactionHolds_RemovesAllOfThemAndOnlyThatChilds()
    {
        var sibling = Guid.NewGuid();
        await Repository.StartAttemptAsync(new ChapterRef(FamilyId, sibling, 6, "gegp101"), Guid.NewGuid(), 3, "v1", Day, CancellationToken.None);
        foreach (var n in Enumerable.Range(1, 120))
        {
            var chapter = new ChapterRef(FamilyId, ChildId, 6, $"ch{n:000}");
            await Repository.StartAttemptAsync(chapter, Guid.NewGuid(), 3, "v1", Day, CancellationToken.None);
            await Repository.RecordCheckAsync(chapter, Guid.NewGuid(), Day, Marks(true), "v1", CancellationToken.None);
        }

        await Repository.RemoveChildAsync(FamilyId, ChildId, CancellationToken.None);
        await Repository.RemoveChildAsync(FamilyId, ChildId, CancellationToken.None);

        Assert.Empty(await Repository.ChaptersAsync(FamilyId, ChildId, 6, CancellationToken.None));
        Assert.Empty(await Repository.AttemptsAsync(new ChapterRef(FamilyId, ChildId, 6, "ch001"), CancellationToken.None));
        Assert.Single(await Repository.ChaptersAsync(FamilyId, sibling, 6, CancellationToken.None));
    }

    public void Dispose() => harness.Dispose();
}
