using Lantern.Core.Constants;

namespace Lantern.Core.Models;

public sealed class Family : IFamilyModel
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; }

    public BoardType Board { get; set; }

    // The Family key, wrapped on the phone twice (D66): Lantern holds the copies and cannot open them.
    public string PassphraseWrappedKey { get; set; }

    public string PassphraseSalt { get; set; }

    public string RecoveryWrappedKey { get; set; }

    public string RecoverySalt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Parent Parent { get; set; }

    public IReadOnlyList<Child> Children { get; set; } = [];
}
