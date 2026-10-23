using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Lantern.Core.Configuration;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Lantern.Core.Storage;
using Lantern.Repository.Entities;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Test.Integration.Host;

internal sealed class SampleProgress(
    TableServiceClient tables,
    BlobServiceClient blobs,
    IOptions<StorageOptions> storage,
    ContentLayout layout,
    IJsonService json,
    IWorkspaceStore workspaces,
    IProgressRepository progress,
    TimeProvider clock
)
{
    private const int GenericConceptCount = 5;
    private const int MinimumConcepts = 3;
    private const string ActiveStatus = "Active";

    public async Task<int> SeedAsync(Guid? onlyChild, bool reset, CancellationToken cancellationToken)
    {
        var seeded = 0;
        await foreach (var child in tables
            .GetTableClient(storage.Value.FamiliesTable)
            .QueryAsync<ChildEntity>(row => row.RowKey.CompareTo("child_") >= 0 && row.RowKey.CompareTo("child`") < 0, cancellationToken: cancellationToken))
        {
            if (child.Status == ActiveStatus && (onlyChild is null || onlyChild == child.ChildId) && await SeedChildAsync(Guid.Parse(child.PartitionKey), child.ChildId, child.ClassLevel, reset, cancellationToken))
            {
                seeded++;
            }
        }

        return seeded;
    }

    private async Task<bool> SeedChildAsync(Guid familyId, Guid childId, int classLevel, bool reset, CancellationToken cancellationToken)
    {
        var manifest = await workspaces.ClassManifestAsync(familyId, childId, classLevel, cancellationToken);
        var subject = manifest?.Subjects.Find(candidate => candidate.Chapters.Count > 0);
        if (subject is null)
        {
            return false;
        }

        if (reset)
        {
            await progress.RemoveChildAsync(familyId, childId, cancellationToken);
        }

        if ((await progress.ChaptersAsync(familyId, childId, classLevel, cancellationToken)).Count > 0)
        {
            return false;
        }

        async Task<(ChapterRef Chapter, IReadOnlyList<RefinedConcept> Concepts)> OpenAsync(int index)
        {
            var chapter = subject.Chapters[index];

            return (new ChapterRef(familyId, childId, classLevel, chapter.ChapterId), await ConceptsAsync(familyId, childId, classLevel, chapter, cancellationToken));
        }

        var (first, firstConcepts) = await OpenAsync(0);
        await PlanAsync(first, firstConcepts, firstConcepts.Count, 18, cancellationToken);
        await CheckAsync(first, firstConcepts, 2, 15, cancellationToken);
        await CheckAsync(first, firstConcepts, 1, 11, cancellationToken);
        await PlanAsync(first, firstConcepts, 2, 3, cancellationToken);

        if (subject.Chapters.Count > 1)
        {
            var (second, secondConcepts) = await OpenAsync(1);
            await PlanAsync(second, secondConcepts, secondConcepts.Count, 12, cancellationToken);
            await CheckAsync(second, secondConcepts, 2, 9, cancellationToken);
        }

        if (subject.Chapters.Count > 2)
        {
            var (third, thirdConcepts) = await OpenAsync(2);
            await PlanAsync(third, thirdConcepts, thirdConcepts.Count, 5, cancellationToken);
        }

        if (subject.Chapters.Count > 3)
        {
            var (fourth, fourthConcepts) = await OpenAsync(3);
            await PlanAsync(fourth, fourthConcepts, 1, 1, cancellationToken);
        }

        return true;
    }

    private async Task<IReadOnlyList<RefinedConcept>> ConceptsAsync(Guid familyId, Guid childId, int classLevel, Chapter chapter, CancellationToken cancellationToken)
    {
        var path = layout.ContentFolder(familyId, childId, classLevel) + layout.RefinedFolder(chapter.BookId) + chapter.ChapterId + layout.FileExtension;
        try
        {
            var blob = await blobs.GetBlobContainerClient(storage.Value.WorkspaceContainer).GetBlobClient(path).DownloadContentAsync(cancellationToken);
            var concepts = json.Deserialize<RefinedChapter>(blob.Value.Content.ToString()).Concepts;
            if (concepts.Count >= MinimumConcepts && concepts.TrueForAll(concept => !string.IsNullOrWhiteSpace(concept.Id) && !string.IsNullOrWhiteSpace(concept.Name)))
            {
                return concepts;
            }
        }
        catch (Exception ex) when (ex is RequestFailedException or System.Text.Json.JsonException or ArgumentException)
        {
        }

        return
        [
            .. Enumerable
                .Range(1, GenericConceptCount)
                .Select(number => new RefinedConcept { Id = $"{chapter.ChapterId}-c{number}", Name = $"Concept {number}" }),
        ];
    }

    private async Task PlanAsync(ChapterRef chapter, IReadOnlyList<RefinedConcept> concepts, int done, int daysAgo, CancellationToken cancellationToken)
    {
        var at = clock.GetUtcNow().AddDays(-daysAgo);
        await progress.StartAttemptAsync(chapter, Guid.NewGuid(), concepts.Count, "sample", at, cancellationToken);
        foreach (var number in Enumerable.Range(1, done))
        {
            await progress.MarkConceptDoneAsync(chapter, concepts[number - 1].Id, at.AddMinutes(number * 10), cancellationToken);
        }
    }

    private async Task CheckAsync(ChapterRef chapter, IReadOnlyList<RefinedConcept> concepts, int wrong, int daysAgo, CancellationToken cancellationToken) =>
        await progress.RecordCheckAsync(
            chapter,
            Guid.NewGuid(),
            clock.GetUtcNow().AddDays(-daysAgo),
            [.. concepts.Select((concept, index) => new CheckedConcept { ConceptId = concept.Id, Title = concept.Name, Right = index < concepts.Count - wrong })],
            "sample",
            cancellationToken
        );
}
