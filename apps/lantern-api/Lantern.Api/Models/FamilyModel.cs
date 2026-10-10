using Lantern.Core.Models;

namespace Lantern.Api.Models;

public sealed class FamilyModel
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; } = string.Empty;

    public BoardType Board { get; set; }

    public string PassphraseWrappedKey { get; set; } = string.Empty;

    public string PassphraseSalt { get; set; } = string.Empty;

    public string RecoveryWrappedKey { get; set; } = string.Empty;

    public string RecoverySalt { get; set; } = string.Empty;

    public ParentModel Parent { get; set; } = new();

    public List<ChildModel> Children { get; set; } = [];
}
