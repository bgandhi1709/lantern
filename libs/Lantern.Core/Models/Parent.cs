namespace Lantern.Core.Models;

public sealed class Parent : IFamilyModel
{
    public Guid ParentId { get; set; }

    public Guid FamilyId { get; set; }

    // Locked on the phone with the Family key (D66).
    public string Name { get; set; }

    public string Email { get; set; }

    public string Language { get; set; }

    public string ConsentVersion { get; set; }

    public DateTimeOffset ConsentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
