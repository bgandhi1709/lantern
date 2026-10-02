using System.ComponentModel.DataAnnotations;

namespace Lantern.Core.Configuration;

// The same section the Functions trigger reads, so one namespace setting serves both apps.
public sealed class ServiceBusOptions : IValidatableObject
{
    public const string SectionName = "ServiceBus";

    // Local development only: the Service Bus emulator has no identity to sign in with.
    public string ConnectionString { get; set; } = string.Empty;

    // Azure: the API signs in with its managed identity.
    public string FullyQualifiedNamespace { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ConnectionString) == string.IsNullOrWhiteSpace(FullyQualifiedNamespace))
        {
            yield return new ValidationResult(
                "Set exactly one of ServiceBus:ConnectionString or ServiceBus:FullyQualifiedNamespace.",
                [nameof(ConnectionString), nameof(FullyQualifiedNamespace)]
            );
        }
    }
}
