using Lantern.Core.Constants;

namespace Lantern.Core.Repository;

public interface IWorkspaceStore
{
    /// <summary>Creates the Child's Workspace folder for the Class; a repeated Class reuses it and changes nothing.</summary>
    Task CreateAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken);

    /// <summary>
    /// Copies the runtime files of the Board's Books for the Class into the Class folder's <c>content/</c> (ADR-0013), and
    /// writes <c>content/version.json</c> last: until it exists the slice is not ready. Safe to repeat: a ready slice is left
    /// as it is, and a half-made one is made again from the start.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The Board's master has no Book for the Class, or lacks a file its rules require. Nothing is marked ready.
    /// </exception>
    Task CopyContentAsync(Guid familyId, Guid childId, int classLevel, BoardType board, CancellationToken cancellationToken);

    /// <summary>Removes the Child's whole Workspace, every Class in it. Safe to repeat.</summary>
    Task RemoveAsync(Guid familyId, Guid childId, CancellationToken cancellationToken);

    /// <summary>Removes the Workspace of every Child of the Family. Safe to repeat.</summary>
    Task RemoveFamilyAsync(Guid familyId, CancellationToken cancellationToken);
}
