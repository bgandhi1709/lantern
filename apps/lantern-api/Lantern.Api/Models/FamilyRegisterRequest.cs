using System.ComponentModel.DataAnnotations;
using Lantern.Core.Models;

namespace Lantern.Api.Models;

public sealed class FamilyRegisterRequest
{
    // The phone makes the Family id before registering: the first Child is locked in the same call.
    [Required]
    public Guid? FamilyId { get; set; }

    // Locked on the phone (D66), as the Parent's name and email below.
    [Required]
    [StringLength(LockedValue.MaxLength, MinimumLength = 1)]
    public string ParentNameLocked { get; set; } = string.Empty;

    [Required]
    [StringLength(LockedValue.MaxLength, MinimumLength = 1)]
    public string ParentEmailLocked { get; set; } = string.Empty;

    // No default: the Parent picks a Board. Nullable so a missing value is refused, not read as the first one.
    [Required]
    public BoardType? Board { get; set; }

    // The Family key wrapped on the phone, once by the Passphrase and once by the Recovery code, with their salts.
    [Required]
    [StringLength(LockedValue.MaxLength, MinimumLength = 1)]
    public string PassphraseWrappedKey { get; set; } = string.Empty;

    [Required]
    [StringLength(LockedValue.MaxLength, MinimumLength = 1)]
    public string PassphraseSalt { get; set; } = string.Empty;

    [Required]
    [StringLength(LockedValue.MaxLength, MinimumLength = 1)]
    public string RecoveryWrappedKey { get; set; } = string.Empty;

    [Required]
    [StringLength(LockedValue.MaxLength, MinimumLength = 1)]
    public string RecoverySalt { get; set; } = string.Empty;

    [Required]
    [StringLength(60, MinimumLength = 1)]
    [RegularExpression(TextPatterns.NoControlCharacters)]
    public string Region { get; set; } = string.Empty;

    [Required]
    [AllowedValues("en", "gu", "hi")]
    public string Language { get; set; } = string.Empty;

    [Required]
    public ConsentModel Consent { get; set; } = new();

    [Required]
    [MinLength(1)]
    [MaxLength(Child.MaxPerFamily)]
    public List<ChildSaveModel> Children { get; set; } = [];
}
