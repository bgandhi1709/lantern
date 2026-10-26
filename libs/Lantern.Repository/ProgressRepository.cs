using Azure;
using Azure.Data.Tables;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Lantern.Repository.Entities;
using Lantern.Repository.UnitOfWork;

namespace Lantern.Repository;

internal sealed class ProgressRepository(
    IUnitOfWork<ChapterProgressEntity> chapters,
    IUnitOfWork<AttemptEntity> attempts,
    IUnitOfWork<CheckEntity> checks,
    IRowKeyService keys,
    IJsonService json
) : IProgressRepository
{
    private const int MaxWriteAttempts = 12;
    private const int MaxTransactionRows = 100;

    public async Task<IReadOnlyList<ChapterProgress>> ChaptersAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken)
    {
        var rows = await chapters.PartitionAsync(keys.FamilyPartition(familyId), keys.ChapterProgressRowPrefix(childId, classLevel), cancellationToken);

        return [.. rows.Select(ToModel)];
    }

    public Task RemoveChildAsync(Guid familyId, Guid childId, CancellationToken cancellationToken) =>
        RemoveAsync(familyId, keys.ProgressRowPrefixesOf(childId), cancellationToken);

    public Task RemoveFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        RemoveAsync(familyId, keys.ProgressRowPrefixes, cancellationToken);

    public async Task<ChapterProgress> ChapterAsync(ChapterRef chapter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var row = await chapters.SingleOrNullAsync(
            keys.FamilyPartition(chapter.FamilyId),
            keys.ChapterProgressRowKey(chapter.ChildId, chapter.ClassLevel, chapter.ChapterId),
            cancellationToken
        );

        return row is null ? null : ToModel(row);
    }

    public async Task<IReadOnlyList<Attempt>> AttemptsAsync(ChapterRef chapter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var partition = keys.FamilyPartition(chapter.FamilyId);
        var attemptRows = await attempts.PartitionAsync(partition, keys.AttemptRowPrefix(chapter.ChildId, chapter.ClassLevel, chapter.ChapterId), cancellationToken);
        var checkRows = await checks.PartitionAsync(partition, keys.CheckRowPrefix(chapter.ChildId, chapter.ClassLevel, chapter.ChapterId), cancellationToken);

        return
        [
            .. attemptRows
                .OrderByDescending(row => row.AttemptNumber)
                .Select(row => new Attempt
                {
                    AttemptNumber = row.AttemptNumber,
                    PlannedAt = row.PlannedAt,
                    ConceptCount = row.ConceptCount,
                    ConceptsDone = row.ConceptsDone,
                    Checks =
                    [
                        .. checkRows
                            .Where(check => check.AttemptNumber == row.AttemptNumber)
                            .OrderByDescending(check => check.CheckNumber)
                            .Select(check => new Check
                            {
                                CheckId = check.CheckId,
                                CheckNumber = check.CheckNumber,
                                TakenAt = check.TakenAt,
                                Right = check.Right,
                                Of = check.Of,
                                Results = json.Deserialize<List<CheckedConcept>>(check.Results),
                            }),
                    ],
                }),
        ];
    }

    public async Task StartAttemptAsync(
        ChapterRef chapter,
        Guid planId,
        int conceptCount,
        string contentVersion,
        DateTimeOffset plannedAt,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var partition = keys.FamilyPartition(chapter.FamilyId);
        var summaryKey = keys.ChapterProgressRowKey(chapter.ChildId, chapter.ClassLevel, chapter.ChapterId);

        for (var attempt = 0; attempt < MaxWriteAttempts; attempt++)
        {
            var summary = await chapters.SingleOrNullAsync(partition, summaryKey, cancellationToken);
            if (summary is not null && await IsPlanOfLatestAttemptAsync(chapter, partition, summary, planId, cancellationToken))
            {
                return;
            }

            var number = (summary?.LatestAttempt ?? 0) + 1;
            var row = new AttemptEntity
            {
                PartitionKey = partition,
                RowKey = keys.AttemptRowKey(chapter.ChildId, chapter.ClassLevel, chapter.ChapterId, number),
                ChildId = chapter.ChildId,
                ClassLevel = chapter.ClassLevel,
                ChapterId = chapter.ChapterId,
                AttemptNumber = number,
                PlanId = planId,
                PlannedAt = plannedAt,
                ConceptCount = conceptCount,
                ContentVersion = contentVersion,
            };
            var next = summary ?? new ChapterProgressEntity
            {
                PartitionKey = partition,
                RowKey = summaryKey,
                ChildId = chapter.ChildId,
                ClassLevel = chapter.ClassLevel,
                ChapterId = chapter.ChapterId,
            };
            next.AttemptCount = number;
            next.LatestAttempt = number;
            next.ConceptCount = conceptCount;
            next.ConceptsDone = 0;
            next.LatestAttemptChecks = 0;
            next.LastActivityAt = plannedAt;

            var written = await TrySubmitAsync(
                [
                    new TableTransactionAction(TableTransactionActionType.Add, row),
                    summary is null
                        ? new TableTransactionAction(TableTransactionActionType.Add, next)
                        : new TableTransactionAction(TableTransactionActionType.UpdateReplace, next, summary.ETag),
                ],
                cancellationToken
            );
            if (written)
            {
                return;
            }
        }

        throw LostTheRace();
    }

    public async Task MarkConceptDoneAsync(ChapterRef chapter, string conceptId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var partition = keys.FamilyPartition(chapter.FamilyId);

        for (var attempt = 0; attempt < MaxWriteAttempts; attempt++)
        {
            var summary =
                await chapters.SingleOrNullAsync(partition, keys.ChapterProgressRowKey(chapter.ChildId, chapter.ClassLevel, chapter.ChapterId), cancellationToken)
                ?? throw new InvalidOperationException("The Chapter has no Attempt.");
            var latest = await attempts.SingleOrNullAsync(
                partition,
                keys.AttemptRowKey(chapter.ChildId, chapter.ClassLevel, chapter.ChapterId, summary.LatestAttempt),
                cancellationToken
            );
            var done = json.Deserialize<Dictionary<string, DateTimeOffset>>(latest.DoneConcepts);
            if (done.ContainsKey(conceptId))
            {
                return;
            }

            if (done.Count >= latest.ConceptCount)
            {
                throw new InvalidOperationException("Every Concept of the Plan is already done.");
            }

            done[conceptId] = at;
            latest.DoneConcepts = json.Serialize(done);
            latest.ConceptsDone = done.Count;
            summary.ConceptsDone = done.Count;
            summary.LastActivityAt = at;

            var written = await TrySubmitAsync(
                [
                    new TableTransactionAction(TableTransactionActionType.UpdateReplace, latest, latest.ETag),
                    new TableTransactionAction(TableTransactionActionType.UpdateReplace, summary, summary.ETag),
                ],
                cancellationToken
            );
            if (written)
            {
                return;
            }
        }

        throw LostTheRace();
    }

    public async Task RecordCheckAsync(
        ChapterRef chapter,
        Guid checkId,
        DateTimeOffset takenAt,
        IReadOnlyList<CheckedConcept> results,
        string contentVersion,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(results);
        if (results.Count == 0)
        {
            throw new ArgumentException("A Check has at least one question.", nameof(results));
        }

        var partition = keys.FamilyPartition(chapter.FamilyId);

        for (var attempt = 0; attempt < MaxWriteAttempts; attempt++)
        {
            var summary =
                await chapters.SingleOrNullAsync(partition, keys.ChapterProgressRowKey(chapter.ChildId, chapter.ClassLevel, chapter.ChapterId), cancellationToken)
                ?? throw new InvalidOperationException("The Chapter has no Attempt.");
            var checkKey = keys.CheckRowKey(chapter.ChildId, chapter.ClassLevel, chapter.ChapterId, summary.LatestAttempt, checkId);
            if (await checks.SingleOrNullAsync(partition, checkKey, cancellationToken) is not null)
            {
                return;
            }

            var right = results.Count(result => result.Right);
            var row = new CheckEntity
            {
                PartitionKey = partition,
                RowKey = checkKey,
                ChildId = chapter.ChildId,
                ClassLevel = chapter.ClassLevel,
                ChapterId = chapter.ChapterId,
                AttemptNumber = summary.LatestAttempt,
                CheckId = checkId,
                CheckNumber = summary.LatestAttemptChecks + 1,
                TakenAt = takenAt,
                Right = right,
                Of = results.Count,
                Results = json.Serialize(results),
                ContentVersion = contentVersion,
            };
            summary.LatestAttemptChecks = row.CheckNumber;
            summary.CheckCount += 1;
            summary.LatestCheckAt = takenAt;
            summary.LatestCheckRight = right;
            summary.LatestCheckOf = results.Count;
            summary.LastActivityAt = takenAt;

            var written = await TrySubmitAsync(
                [
                    new TableTransactionAction(TableTransactionActionType.Add, row),
                    new TableTransactionAction(TableTransactionActionType.UpdateReplace, summary, summary.ETag),
                ],
                cancellationToken
            );
            if (written)
            {
                return;
            }
        }

        throw LostTheRace();
    }

    private async Task RemoveAsync(Guid familyId, IReadOnlyList<string> prefixes, CancellationToken cancellationToken)
    {
        var partition = keys.FamilyPartition(familyId);

        for (var attempt = 0; attempt < MaxWriteAttempts; attempt++)
        {
            List<ITableEntity> rows =
            [
                .. await chapters.PartitionAsync(partition, prefixes[0], cancellationToken),
                .. await attempts.PartitionAsync(partition, prefixes[1], cancellationToken),
                .. await checks.PartitionAsync(partition, prefixes[2], cancellationToken),
            ];

            try
            {
                foreach (var batch in rows.Chunk(MaxTransactionRows))
                {
                    await chapters.SubmitAsync(batch.Select(row => new TableTransactionAction(TableTransactionActionType.Delete, row, ETag.All)), cancellationToken);
                }

                return;
            }
            catch (RequestFailedException ex) when (ex.Status is 404 or 412)
            {
            }
        }

        throw LostTheRace();
    }

    private async Task<bool> TrySubmitAsync(IEnumerable<TableTransactionAction> actions, CancellationToken cancellationToken)
    {
        try
        {
            await chapters.SubmitAsync(actions, cancellationToken);

            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            return false;
        }
    }

    private static InvalidOperationException LostTheRace() =>
        new("The Chapter's progress kept changing while it was being written.");

    private async Task<bool> IsPlanOfLatestAttemptAsync(ChapterRef chapter, string partition, ChapterProgressEntity summary, Guid planId, CancellationToken cancellationToken)
    {
        var latest = await attempts.SingleOrNullAsync(
            partition,
            keys.AttemptRowKey(chapter.ChildId, chapter.ClassLevel, chapter.ChapterId, summary.LatestAttempt),
            cancellationToken
        );

        return latest?.PlanId == planId;
    }

    private static ChapterProgress ToModel(ChapterProgressEntity row) =>
        new()
        {
            ChapterId = row.ChapterId,
            AttemptCount = row.AttemptCount,
            LatestAttempt = row.LatestAttempt,
            ConceptCount = row.ConceptCount,
            ConceptsDone = row.ConceptsDone,
            LatestAttemptChecks = row.LatestAttemptChecks,
            CheckCount = row.CheckCount,
            LatestCheckAt = row.LatestCheckAt,
            LatestCheckRight = row.LatestCheckRight,
            LatestCheckOf = row.LatestCheckOf,
            LastActivityAt = row.LastActivityAt,
        };
}
