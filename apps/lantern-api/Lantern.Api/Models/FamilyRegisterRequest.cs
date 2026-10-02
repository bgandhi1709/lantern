using System.ComponentModel.DataAnnotations;
using Lantern.Core.Models;

namespace Lantern.Api.Models;

public sealed class FamilyRegisterRequest
{
    [Required]
    [StringLength(60, MinimumLength = 1)]
    [RegularExpression(TextPatterns.NoControlCharacters)]
    public string Region { get; set; } = string.Empty;

    [Required]
    [AllowedValues("en", "gu", "hi")]
    public string Language { get; set; } = string.Empty;

    [Required]
    public ConsentModel Consent { get; set; } = new();

    [Required]
    [MinLength(1)]
    [MaxLength(Child.MaxPerFamily)]
    public List<ChildSaveModel> Children { get; set; } = [];
}
