using Lantern.Core.Models;

namespace Lantern.Base.Services;

public interface ISubjectService
{
    Task<Page<Subject>> SubjectsAsync(Guid childId, int? limit, string cursor, CancellationToken cancellationToken);

    Task<ChapterAttempts> AttemptsAsync(Guid childId, string chapterId, int? limit, string cursor, CancellationToken cancellationToken);

    Task<SubjectChapters> ChaptersAsync(Guid childId, string subjectId, string query, int? limit, string cursor, CancellationToken cancellationToken);
}
