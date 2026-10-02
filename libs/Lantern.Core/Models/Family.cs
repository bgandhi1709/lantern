namespace Lantern.Core.Models;

public sealed class Family : IFamilyModel
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public Parent? Parent { get; set; }

    public IReadOnlyList<Child> Children { get; set; } = [];
}
