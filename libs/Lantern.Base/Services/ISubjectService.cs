using Lantern.Core.Models;

namespace Lantern.Base.Services;

public interface ISubjectService
{
    Task<Page<Subject>> SubjectsAsync(Guid childId, int? limit, string cursor, CancellationToken cancellationToken);

    Task<Page<Chapter>> ChaptersAsync(Guid childId, string subjectId, int? limit, string cursor, CancellationToken cancellationToken);
}
