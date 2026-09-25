using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Configuration;

// Changing the key orphans every registration, so never rotate it without a migration. Key Vault envelope encryption (#23) replaces this.
public sealed class SecurityOptions : IValidatableObject
{
    public const string SectionName = "Security";

    public const int MinimumKeyBytes = 32;

    [Required]
    public string Key { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var buffer = new byte[Key.Length];

        if (!Convert.TryFromBase64String(Key, buffer, out var written) || written < MinimumKeyBytes)
        {
            yield return new ValidationResult(
                $"Security:Key must be base64 and at least {MinimumKeyBytes} bytes.",
                [nameof(Key)]
            );
        }
    }
}
