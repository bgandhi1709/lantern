using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Functions.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Functions.Tests;

[Collection(FunctionsCollection.Name)]
public sealed class ProgressSweepTests(AzuriteFixture azurite) : IDisposable
{
    private static readonly DateTimeOffset Day = new(2026, 10, 1, 16, 5, 40, TimeSpan.Zero);

    private readonly FunctionsHost host = new(azurite.ConnectionString);

    [Fact]
    public async Task DeletingAChild_RemovesItsProgressInEveryClass_AndNothingOfItsSiblingOrAnotherFamily()
    {
        using var caller = host.Caller(NewUid());
        using var neighbour = host.Caller(NewUid());
        var family = await caller.RegisterAsync(BoardType.Cbse, 7, 7);
        var other = await neighbour.RegisterAsync(BoardType.Cbse, 7);
        var (leaving, staying) = (family.Children[0].ChildId, family.Children[1].ChildId);
        await WorkedOnAsync(family.FamilyId, leaving, 7);
        await WorkedOnAsync(family.FamilyId, leaving, 8);
        await WorkedOnAsync(family.FamilyId, staying, 7);
        await WorkedOnAsync(other.FamilyId, other.Children[0].ChildId, 7);

        await caller.Children.RemoveAsync(leaving, CancellationToken.None);
        await host.DeliverAsync();

        Assert.Empty(await ChaptersAsync(family.FamilyId, leaving, 7));
        Assert.Empty(await ChaptersAsync(family.FamilyId, leaving, 8));
        Assert.Empty(await HistoryAsync(family.FamilyId, leaving, 7));
        Assert.Single(await ChaptersAsync(family.FamilyId, staying, 7));
        Assert.Single(await HistoryAsync(family.FamilyId, staying, 7));
        Assert.Single(await ChaptersAsync(other.FamilyId, other.Children[0].ChildId, 7));
    }

    [Fact]
    public async Task DeletingAFamily_RemovesTheProgressOfEveryChild_AndNothingOfAnotherFamily()
    {
        using var caller = host.Caller(NewUid());
        using var neighbour = host.Caller(NewUid());
        var family = await caller.RegisterAsync(BoardType.Cbse, 7, 7);
        var other = await neighbour.RegisterAsync(BoardType.Cbse, 7);
        foreach (var child in family.Children)
        {
            await WorkedOnAsync(family.FamilyId, child.ChildId, 7);
        }

        await WorkedOnAsync(other.FamilyId, other.Children[0].ChildId, 7);

        await caller.EraseFamilyAsync();
        await host.DeliverAsync();

        foreach (var child in family.Children)
        {
            Assert.Empty(await ChaptersAsync(family.FamilyId, child.ChildId, 7));
        }

        Assert.Single(await ChaptersAsync(other.FamilyId, other.Children[0].ChildId, 7));
    }

    [Fact]
    public async Task DeletingAChild_TwiceOver_StillLeavesNothing()
    {
        using var caller = host.Caller(NewUid());
        var family = await caller.RegisterAsync(BoardType.Cbse, 7);
        var child = family.Children[0].ChildId;
        await WorkedOnAsync(family.FamilyId, child, 7);
        await caller.Children.RemoveAsync(child, CancellationToken.None);
        await host.DeliverAsync();

        await host.Services.DeliverAsync(host.Sender.Sent[^1]);

        Assert.Empty(await ChaptersAsync(family.FamilyId, child, 7));
    }

    public void Dispose() => host.Dispose();

    private async Task WorkedOnAsync(Guid familyId, Guid childId, int classLevel)
    {
        using var scope = host.Services.CreateScope();
        var progress = scope.ServiceProvider.GetRequiredService<IProgressRepository>();
        var chapter = new ChapterRef(familyId, childId, classLevel, "gegp101");
        await progress.StartAttemptAsync(chapter, Guid.NewGuid(), 2, "v1", Day, CancellationToken.None);
        await progress.RecordCheckAsync(chapter, Guid.NewGuid(), Day, [new CheckedConcept { ConceptId = "c1", Title = "Concept 1", Right = true }], "v1", CancellationToken.None);
    }

    private async Task<IReadOnlyList<ChapterProgress>> ChaptersAsync(Guid familyId, Guid childId, int classLevel)
    {
        using var scope = host.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IProgressRepository>().ChaptersAsync(familyId, childId, classLevel, CancellationToken.None);
    }

    private async Task<IReadOnlyList<Attempt>> HistoryAsync(Guid familyId, Guid childId, int classLevel)
    {
        using var scope = host.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IProgressRepository>().AttemptsAsync(new ChapterRef(familyId, childId, classLevel, "gegp101"), CancellationToken.None);
    }

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";
}
