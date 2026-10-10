using Lantern.Core.Constants;

namespace Lantern.Core.Models;

public sealed class Registration
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; }

    public BoardType Board { get; set; }

    public string PassphraseWrappedKey { get; set; }

    public string PassphraseSalt { get; set; }

    public string RecoveryWrappedKey { get; set; }

    public string RecoverySalt { get; set; }

    public string ParentName { get; set; }

    public string ParentEmail { get; set; }

    public string Language { get; set; }

    public bool ConsentAccepted { get; set; }

    public string ConsentNoticeVersion { get; set; }

    public IReadOnlyList<Child> Children { get; set; } = [];
}
