using System.ComponentModel.DataAnnotations;

namespace Lantern.Core.Configuration;

public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    [Required]
    public string Database { get; set; } = string.Empty;

    [Required]
    public string ContentContainer { get; set; } = string.Empty;
}
