namespace Lantern.Core.Models;

public sealed class Registration
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; } = string.Empty;

    public BoardType Board { get; set; }

    public string PassphraseWrappedKey { get; set; } = string.Empty;

    public string PassphraseSalt { get; set; } = string.Empty;

    public string RecoveryWrappedKey { get; set; } = string.Empty;

    public string RecoverySalt { get; set; } = string.Empty;

    public string ParentNameLocked { get; set; } = string.Empty;

    public string ParentEmailLocked { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public bool ConsentAccepted { get; set; }

    public string ConsentNoticeVersion { get; set; } = string.Empty;

    public IReadOnlyList<Child> Children { get; set; } = [];
}
