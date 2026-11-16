using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Service;

namespace Lantern.Base.Services;

internal sealed class SubjectService(
    IServiceBase<Child> children,
    IServiceBase<Family> families,
    IContentRepository content,
    IProgressRepository progress,
    IPageCalculator pages
) : ISubjectService
{
    private const int MaxSearchLength = 64;

    public async Task<Page<Subject>> SubjectsAsync(Guid childId, int? limit, string cursor, CancellationToken cancellationToken)
    {
        var child = await ActiveChildAsync(childId, cancellationToken);

        return pages.Slice((await ClassAsync(child, cancellationToken)).ToSubjects(), limit, cursor);
    }

    public async Task<SubjectChapters> ChaptersAsync(Guid childId, string subjectId, string query, int? limit, string cursor, CancellationToken cancellationToken)
    {
        var search = query?.Trim();
        if (search?.Length > MaxSearchLength)
        {
            throw new LanternException(LanternErrorCode.InvalidSearch);
        }

        var child = await ActiveChildAsync(childId, cancellationToken);
        var chapters = (await ClassAsync(child, cancellationToken)).ChaptersOf(subjectId) ?? throw new LanternException(LanternErrorCode.SubjectNotFound);

        var worked = (await progress.ChaptersAsync(child.FamilyId, child.ChildId, child.ClassLevel, cancellationToken)).ToDictionary(row => row.ChapterId);
        IReadOnlyList<ChapterEntry> entries =
        [
            .. chapters.Select((chapter, index) => new ChapterEntry(chapter.WithNumber(index + 1), worked.GetValueOrDefault(chapter.ChapterId))),
        ];
        IReadOnlyList<ChapterEntry> matching = string.IsNullOrEmpty(search)
            ? entries
            : [.. entries.Where(entry => entry.Title.Contains(search, StringComparison.OrdinalIgnoreCase))];

        return new SubjectChapters(pages.Slice(matching, limit, cursor), new SubjectProgress(entries));
    }

    public async Task<ChapterAttempts> AttemptsAsync(Guid childId, string chapterId, int? limit, string cursor, CancellationToken cancellationToken)
    {
        var child = await ActiveChildAsync(childId, cancellationToken);
        var place = (await ClassAsync(child, cancellationToken)).PlaceOf(chapterId) ?? throw new LanternException(LanternErrorCode.ChapterNotFound);
        var reference = new ChapterRef(child.FamilyId, child.ChildId, child.ClassLevel, chapterId);
        var summary = await progress.ChapterAsync(reference, cancellationToken);
        var history = await progress.AttemptsAsync(reference, cancellationToken);

        return new ChapterAttempts(new ChapterHeader(place, summary), pages.Slice(history, limit, cursor));
    }

    private async Task<Child> ActiveChildAsync(Guid childId, CancellationToken cancellationToken)
    {
        var child = await children.SingleAsync(childId, cancellationToken);

        return child.Status == ChildStatus.Deleting ? throw new LanternException(LanternErrorCode.ChildNotFound) : child;
    }

    private async Task<ClassManifest> ClassAsync(Child child, CancellationToken cancellationToken)
    {
        var family = await families.SingleAsync(child.FamilyId, cancellationToken);

        return await content.ClassAsync(family.Board, child.ClassLevel, cancellationToken) ?? throw new LanternException(LanternErrorCode.ContentNotReady);
    }
}
