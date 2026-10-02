namespace Lantern.Core.Models;

public sealed class Parent : IFamilyModel
{
    public Guid ParentId { get; set; }

    public Guid FamilyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string ConsentVersion { get; set; } = string.Empty;

    public DateTimeOffset ConsentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
