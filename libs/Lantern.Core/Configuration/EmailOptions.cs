using System.ComponentModel.DataAnnotations;

namespace Lantern.Core.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string Sender { get; set; } = string.Empty;
}
