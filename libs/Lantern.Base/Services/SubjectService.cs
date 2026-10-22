using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Service;

namespace Lantern.Base.Services;

internal sealed class SubjectService(IServiceBase<Child> children, IWorkspaceStore workspaces, IPageCalculator pages) : ISubjectService
{
    public async Task<Page<Subject>> SubjectsAsync(Guid childId, int? limit, string cursor, CancellationToken cancellationToken)
    {
        var child = await ActiveChildAsync(childId, cancellationToken);

        return pages.Slice(await ReadyAsync(child, cancellationToken), limit, cursor);
    }

    public async Task<Page<Chapter>> ChaptersAsync(Guid childId, string subjectId, int? limit, string cursor, CancellationToken cancellationToken)
    {
        var child = await ActiveChildAsync(childId, cancellationToken);
        if (!(await ReadyAsync(child, cancellationToken)).Any(subject => subject.SubjectId == subjectId))
        {
            throw new LanternException(LanternErrorCode.SubjectNotFound);
        }

        var chapters =
            await workspaces.ChaptersAsync(child.FamilyId, child.ChildId, child.ClassLevel, subjectId, cancellationToken)
            ?? throw new LanternException(LanternErrorCode.ContentNotReady);

        return pages.Slice([.. chapters.Select((chapter, index) => chapter.WithNumber(index + 1))], limit, cursor);
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
