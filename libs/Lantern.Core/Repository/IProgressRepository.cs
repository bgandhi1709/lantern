using Lantern.Core.Models;

namespace Lantern.Core.Repository;

public interface IProgressRepository
{
    Task<IReadOnlyList<ChapterProgress>> ChaptersAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken);

    Task<IReadOnlyList<Attempt>> AttemptsAsync(ChapterRef chapter, CancellationToken cancellationToken);

    Task StartAttemptAsync(
        ChapterRef chapter,
        Guid planId,
        int conceptCount,
        string contentVersion,
        DateTimeOffset plannedAt,
        CancellationToken cancellationToken
    );

    Task MarkConceptDoneAsync(ChapterRef chapter, string conceptId, DateTimeOffset at, CancellationToken cancellationToken);

    Task RecordCheckAsync(
        ChapterRef chapter,
        Guid checkId,
        DateTimeOffset takenAt,
        IReadOnlyList<CheckedConcept> results,
        string contentVersion,
        CancellationToken cancellationToken
    );
}
