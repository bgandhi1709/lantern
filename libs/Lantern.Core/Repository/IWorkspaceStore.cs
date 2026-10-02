namespace Lantern.Core.Repository;

public interface IWorkspaceStore
{
    /// <summary>Creates the Child's Workspace folder for the Class; a repeated Class reuses it and changes nothing.</summary>
    Task CreateAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken);

    /// <summary>Removes the Child's whole Workspace, every Class in it. Safe to repeat.</summary>
    Task RemoveAsync(Guid familyId, Guid childId, CancellationToken cancellationToken);
}
