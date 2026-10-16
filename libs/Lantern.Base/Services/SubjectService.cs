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
        var child = await children.SingleAsync(childId, cancellationToken);

        if (child.Status == ChildStatus.Deleting)
        {
            throw new LanternException(LanternErrorCode.ChildNotFound);
        }

        var subjects =
            await workspaces.SubjectsAsync(child.FamilyId, child.ChildId, cancellationToken)
            ?? throw new LanternException(LanternErrorCode.ContentNotReady);

        return pages.Slice(subjects, limit, cursor);
    }
}
