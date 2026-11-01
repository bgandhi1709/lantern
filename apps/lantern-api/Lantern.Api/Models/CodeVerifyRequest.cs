using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Models;

public sealed class CodeVerifyRequest
{
    [Required]
    [StringLength(2000)]
    public string Challenge { get; set; } = string.Empty;

    [Required]
    [RegularExpression(TextPatterns.SixDigits)]
    public string Code { get; set; } = string.Empty;
}
