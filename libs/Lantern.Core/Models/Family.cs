using Lantern.Core.Constants;

namespace Lantern.Core.Models;

public sealed class Family : IFamilyModel
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; }

    public BoardType Board { get; set; }

    public string RecoveryWrappedKey { get; set; }

    public string RecoverySalt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Parent Parent { get; set; }

    public IReadOnlyList<Child> Children { get; set; } = [];
}
