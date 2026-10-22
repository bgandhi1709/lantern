using Azure.Data.Tables;
using Lantern.Core.Configuration;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Repository.Entities;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Test.Integration.Host;

internal sealed class SampleProgress(
    TableServiceClient tables,
    IOptions<StorageOptions> storage,
    IWorkspaceStore workspaces,
    IProgressRepository progress,
    TimeProvider clock
)
{
    private const int ConceptsPerPlan = 5;
    private const string ActiveStatus = "Active";

    public async Task<int> SeedAsync(Guid? onlyChild, CancellationToken cancellationToken)
    {
        var seeded = 0;
        await foreach (var child in tables
            .GetTableClient(storage.Value.FamiliesTable)
            .QueryAsync<ChildEntity>(row => row.RowKey.CompareTo("child_") >= 0 && row.RowKey.CompareTo("child`") < 0, cancellationToken: cancellationToken))
        {
            if (child.Status == ActiveStatus && (onlyChild is null || onlyChild == child.ChildId) && await SeedChildAsync(Guid.Parse(child.PartitionKey), child.ChildId, child.ClassLevel, cancellationToken))
            {
                seeded++;
            }
        }

        return seeded;
    }

    private async Task<bool> SeedChildAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken)
    {
        var manifest = await workspaces.ClassManifestAsync(familyId, childId, classLevel, cancellationToken);
        var subject = manifest?.Subjects.Find(candidate => candidate.Chapters.Count > 0);
        if (subject is null || (await progress.ChaptersAsync(familyId, childId, classLevel, cancellationToken)).Count > 0)
        {
            return false;
        }

        ChapterRef Chapter(int index) => new(familyId, childId, classLevel, subject.Chapters[index].ChapterId);

        await PlanAsync(Chapter(0), ConceptsPerPlan, ConceptsPerPlan, 18, cancellationToken);
        await CheckAsync(Chapter(0), 3, 15, cancellationToken);
        await CheckAsync(Chapter(0), 4, 11, cancellationToken);
        await PlanAsync(Chapter(0), 4, 2, 3, cancellationToken);

        if (subject.Chapters.Count > 1)
        {
            await PlanAsync(Chapter(1), ConceptsPerPlan, ConceptsPerPlan, 12, cancellationToken);
            await CheckAsync(Chapter(1), 2, 9, cancellationToken);
        }

        if (subject.Chapters.Count > 2)
        {
            await PlanAsync(Chapter(2), ConceptsPerPlan, ConceptsPerPlan, 5, cancellationToken);
        }

        if (subject.Chapters.Count > 3)
        {
            await PlanAsync(Chapter(3), 4, 1, 1, cancellationToken);
        }

        return true;
    }

    private async Task PlanAsync(ChapterRef chapter, int concepts, int done, int daysAgo, CancellationToken cancellationToken)
    {
        var at = clock.GetUtcNow().AddDays(-daysAgo);
        await progress.StartAttemptAsync(chapter, Guid.NewGuid(), concepts, "sample", at, cancellationToken);
        foreach (var number in Enumerable.Range(1, done))
        {
            await progress.MarkConceptDoneAsync(chapter, $"{chapter.ChapterId}-c{number}", at.AddMinutes(number * 10), cancellationToken);
        }
    }

    private async Task CheckAsync(ChapterRef chapter, int right, int daysAgo, CancellationToken cancellationToken) =>
        await progress.RecordCheckAsync(
            chapter,
            Guid.NewGuid(),
            clock.GetUtcNow().AddDays(-daysAgo),
            [
                .. Enumerable
                    .Range(1, ConceptsPerPlan)
                    .Select(number => new CheckedConcept { ConceptId = $"{chapter.ChapterId}-c{number}", Title = $"Concept {number}", Right = number <= right }),
            ],
            "sample",
            cancellationToken
        );
}
