using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Models;

public sealed class CodeIssueRequest
{
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression(TextPatterns.KebabName)]
    public string Operation { get; set; } = string.Empty;

    [StringLength(64)]
    [RegularExpression(TextPatterns.NoControlCharactersOrEmpty)]
    public string Target { get; set; }
}
