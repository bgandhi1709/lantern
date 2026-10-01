using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Configuration;

public sealed class KeyVaultOptions : IValidatableObject
{
    public const string SectionName = "KeyVault";

    public string VaultUri { get; set; } = string.Empty;

    public string FamilyKeyName { get; set; } = string.Empty;

    // Local Docker only: a PEM file stands in for the vault. LocalDevelopmentGuard keeps it out of every other environment.
    public string LocalKeyPath { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasVault = !string.IsNullOrWhiteSpace(VaultUri) && !string.IsNullOrWhiteSpace(FamilyKeyName);

        if (!hasVault && string.IsNullOrWhiteSpace(LocalKeyPath))
        {
            yield return new ValidationResult(
                "Set KeyVault:VaultUri and KeyVault:FamilyKeyName, or KeyVault:LocalKeyPath for local development.",
                [nameof(VaultUri), nameof(FamilyKeyName), nameof(LocalKeyPath)]
            );
        }
    }
}
