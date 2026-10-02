namespace Lantern.Core.Models;

/// <summary>A model stored inside one Family. The generic service sets the Family from the caller, never the request.</summary>
public interface IFamilyModel
{
    Guid FamilyId { get; set; }
}
