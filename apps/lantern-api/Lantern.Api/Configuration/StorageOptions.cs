using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Configuration;

public sealed class StorageOptions : IValidatableObject
{
    public const string SectionName = "Storage";

    public string ConnectionString { get; set; } = string.Empty;

    public string TableEndpoint { get; set; } = string.Empty;

    [Required]
    public string ParentsTable { get; set; } = "parents";

    [Required]
    public string FamiliesTable { get; set; } = "families";

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
    }
}
