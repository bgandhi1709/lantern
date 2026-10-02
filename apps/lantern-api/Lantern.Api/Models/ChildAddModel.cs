using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Models;

public sealed class ChildAddModel : ChildSaveModel
{
    // The client picks the id, so a repeated request returns the same Child instead of adding a second one.
    [Required]
    public Guid ChildId { get; set; }
}
