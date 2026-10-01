using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Configuration;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    [Required]
    public string ProjectId { get; set; } = string.Empty;

    // Local Docker only: accept the Firebase Auth Emulator's unsigned tokens. LocalDevelopmentGuard keeps it out of every other environment.
    public string EmulatorHost { get; set; } = string.Empty;
}
