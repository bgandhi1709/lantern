using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Models;

// What an edit may change. The Class is not here: it changes only when a Class starts.
public class ChildUpdateModel
{
    // Locked on the phone (D66): Lantern stores them as sent and cannot read them, so it only bounds their size. School is always sent: the phone locks an empty one.
    [Required]
    [StringLength(400, MinimumLength = 1)]
    public string Name { get; set; }

    [Required]
    [StringLength(400, MinimumLength = 1)]
    public string BirthYear { get; set; }

    [Required]
    [StringLength(400, MinimumLength = 1)]
    public string School { get; set; }
}
