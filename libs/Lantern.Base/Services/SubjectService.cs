using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Service;

namespace Lantern.Base.Services;

internal sealed class SubjectService(IServiceBase<Child> children, IWorkspaceStore workspaces, IProgressRepository progress, IPageCalculator pages) : ISubjectService
{
    private const int MaxSearchLength = 64;

    public async Task<Page<Subject>> SubjectsAsync(Guid childId, int? limit, string cursor, CancellationToken cancellationToken)
    {
        var child = await ActiveChildAsync(childId, cancellationToken);

        return pages.Slice(await ReadyAsync(child, cancellationToken), limit, cursor);
    }

    public async Task<SubjectChapters> ChaptersAsync(Guid childId, string subjectId, string query, int? limit, string cursor, CancellationToken cancellationToken)
    {
        var search = query?.Trim();
        if (search?.Length > MaxSearchLength)
        {
            throw new LanternException(LanternErrorCode.InvalidSearch);
        }

        var child = await ActiveChildAsync(childId, cancellationToken);
        if (!(await ReadyAsync(child, cancellationToken)).Any(subject => subject.SubjectId == subjectId))
        {
            throw new LanternException(LanternErrorCode.SubjectNotFound);
        }

        var chapters =
            await workspaces.ChaptersAsync(child.FamilyId, child.ChildId, child.ClassLevel, subjectId, cancellationToken)
            ?? throw new LanternException(LanternErrorCode.ContentNotReady);

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
        await ReadyAsync(child, cancellationToken);

        var manifest =
            await workspaces.ClassManifestAsync(child.FamilyId, child.ChildId, child.ClassLevel, cancellationToken)
            ?? throw new LanternException(LanternErrorCode.ContentNotReady);
        var place = manifest.PlaceOf(chapterId) ?? throw new LanternException(LanternErrorCode.ChapterNotFound);
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

    private async Task<IReadOnlyList<Subject>> ReadyAsync(Child child, CancellationToken cancellationToken) =>
        await workspaces.SubjectsAsync(child.FamilyId, child.ChildId, cancellationToken)
        ?? throw new LanternException(LanternErrorCode.ContentNotReady);
}
