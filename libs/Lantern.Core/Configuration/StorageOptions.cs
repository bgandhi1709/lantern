using System.ComponentModel.DataAnnotations;

namespace Lantern.Core.Configuration;

public sealed class StorageOptions : IValidatableObject
{
    public const string SectionName = "Storage";

    public string ConnectionString { get; set; } = string.Empty;

    public string TableEndpoint { get; set; } = string.Empty;

    public string BlobEndpoint { get; set; } = string.Empty;

    // Set by the deployment from the resources it creates (infra/params), never defaulted here.
    [Required]
    public string ParentsTable { get; set; } = string.Empty;

    [Required]
    public string FamiliesTable { get; set; } = string.Empty;

    [Required]
    public string ActionsTable { get; set; } = string.Empty;

    [Required]
    public string WorkspaceContainer { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasConnectionString = !string.IsNullOrWhiteSpace(ConnectionString);
        var hasEndpoint = !string.IsNullOrWhiteSpace(TableEndpoint);

        if (hasConnectionString == hasEndpoint)
        {
            yield return new ValidationResult(
                "Set exactly one of Storage:ConnectionString or Storage:TableEndpoint.",
                [nameof(ConnectionString), nameof(TableEndpoint)]
            );
        }

        if (hasEndpoint && string.IsNullOrWhiteSpace(BlobEndpoint))
        {
            yield return new ValidationResult(
                "Set Storage:BlobEndpoint together with Storage:TableEndpoint.",
                [nameof(BlobEndpoint)]
            );
        }
    }
}
