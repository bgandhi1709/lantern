using System.ComponentModel.DataAnnotations;
using Lantern.Core.Constants;
using Lantern.Core.Models;

namespace Lantern.Api.Models;

public sealed class FamilyRegisterRequest
{
    // The phone makes the Family id before registering: the first Child is locked in the same call.
    public Guid FamilyId { get; set; }

    // Locked on the phone (D66), as the Parent's name and email below.
    [Required]
    [StringLength(400, MinimumLength = 1)]
    public string ParentName { get; set; }

    [Required]
    [StringLength(400, MinimumLength = 1)]
    public string ParentEmail { get; set; }

    // No default: the Parent picks a Board. BoardType starts at 1, so a missing value is 0 and is refused here.
    [EnumDataType(typeof(BoardType))]
    public BoardType Board { get; set; }

    // The Family key wrapped on the phone, once by the Passphrase and once by the Recovery code, with their salts.
    [Required]
    [StringLength(400, MinimumLength = 1)]
    public string PassphraseWrappedKey { get; set; }

    [Required]
    [StringLength(400, MinimumLength = 1)]
    public string PassphraseSalt { get; set; }

    [Required]
    [StringLength(400, MinimumLength = 1)]
    public string RecoveryWrappedKey { get; set; }

    [Required]
    [StringLength(400, MinimumLength = 1)]
    public string RecoverySalt { get; set; }

    [Required]
    [StringLength(60, MinimumLength = 1)]
    [RegularExpression(TextPatterns.NoControlCharacters)]
    public string Region { get; set; }

    [Required]
    [AllowedValues("en", "gu", "hi")]
    public string Language { get; set; }

    [Required]
    public ConsentModel Consent { get; set; } = new();

    [Required]
    [MinLength(1)]
    [MaxLength(Child.MaxPerFamily)]
    public List<ChildSaveModel> Children { get; set; } = [];
}
