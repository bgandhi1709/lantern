using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Models;

public sealed class ConsentModel
{
    public bool Accepted { get; set; }

    [Required]
    [StringLength(20, MinimumLength = 1)]
    [RegularExpression(TextPatterns.NoControlCharacters)]
    public string NoticeVersion { get; set; }
}
