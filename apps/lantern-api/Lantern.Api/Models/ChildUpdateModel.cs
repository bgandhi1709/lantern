using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Models;

// What an edit may change. The Class is not here: it changes only when a Class starts.
public class ChildUpdateModel
{
    // Locked on the phone (D66): Lantern stores them as sent and cannot read them, so it only bounds their size.
    [Required]
    [StringLength(LockedValue.MaxLength, MinimumLength = 1)]
    public string NameLocked { get; set; } = string.Empty;

    [Required]
    [StringLength(LockedValue.MaxLength, MinimumLength = 1)]
    public string BirthYearLocked { get; set; } = string.Empty;

    [StringLength(LockedValue.MaxLength)]
    public string? SchoolLocked { get; set; }
}
