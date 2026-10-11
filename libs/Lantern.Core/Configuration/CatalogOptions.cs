using System.ComponentModel.DataAnnotations;

namespace Lantern.Core.Configuration;

// Set by the deployment from the containers it creates (infra/params), never defaulted here. One per Board.
public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    [Required]
    public string NcertContainer { get; set; } = string.Empty;

    [Required]
    public string SscContainer { get; set; } = string.Empty;
}
