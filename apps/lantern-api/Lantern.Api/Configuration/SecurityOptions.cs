using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Configuration;

public sealed class SecurityOptions : IValidatableObject
{
    internal const string SectionName = "Security";

    const int MinimumKeyBytes = 32;

    [Required]
    public string Key { get; init; } = string.Empty;

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
