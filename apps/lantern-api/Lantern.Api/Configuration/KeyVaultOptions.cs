using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Configuration;

public sealed class KeyVaultOptions
{
    public const string SectionName = "KeyVault";

    [Required]
    public string VaultUri { get; set; } = string.Empty;

    [Required]
    public string FamilyKeyName { get; set; } = string.Empty;
}
