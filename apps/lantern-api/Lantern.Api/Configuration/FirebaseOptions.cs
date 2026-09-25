using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Configuration;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    [Required]
    public string ProjectId { get; set; } = string.Empty;
}
