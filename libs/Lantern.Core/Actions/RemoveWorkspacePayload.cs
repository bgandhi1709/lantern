namespace Lantern.Core.Actions;

public sealed record RemoveWorkspacePayload(Guid FamilyId, Guid ChildId);
