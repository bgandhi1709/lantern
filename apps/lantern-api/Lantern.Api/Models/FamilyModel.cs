namespace Lantern.Api.Models;

public sealed class FamilyModel
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; } = string.Empty;

    public ParentModel Parent { get; set; } = new();

    public List<ChildModel> Children { get; set; } = [];
}
