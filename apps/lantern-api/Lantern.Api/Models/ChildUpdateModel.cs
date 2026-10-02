using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Models;

// What an edit may change. The Class is not here: it changes only when a Class starts.
public class ChildUpdateModel
{
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression(TextPatterns.NoControlCharacters)]
    public string Name { get; set; } = string.Empty;

    public int BirthYear { get; set; }

    [StringLength(120)]
    [RegularExpression(TextPatterns.NoControlCharactersOrEmpty)]
    public string? School { get; set; }
}
