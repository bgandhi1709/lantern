using Lantern.Core.Constants;

namespace Lantern.Api.Models;

public sealed class FamilyModel
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; }

    public BoardType Board { get; set; }

    public string PassphraseWrappedKey { get; set; }

    public string PassphraseSalt { get; set; }

    public string RecoveryWrappedKey { get; set; }

    public string RecoverySalt { get; set; }

    public ParentModel Parent { get; set; } = new();

    public List<ChildModel> Children { get; set; } = [];
}
