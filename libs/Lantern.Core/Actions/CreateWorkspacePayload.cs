using System.Globalization;

namespace Lantern.Core.Actions;

public sealed record CreateWorkspacePayload(Guid FamilyId, Guid ChildId, int ClassLevel)
{
    public string ActionId() => string.Create(CultureInfo.InvariantCulture, $"{FamilyId:N}_{ChildId:N}_{ClassLevel}");
}
