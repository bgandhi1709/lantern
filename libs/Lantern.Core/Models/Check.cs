namespace Lantern.Core.Models;

public sealed class Check
{
    public Guid CheckId { get; set; }

    public int CheckNumber { get; set; }

    public DateTimeOffset TakenAt { get; set; }

    public int Right { get; set; }

    public int Of { get; set; }

    public IReadOnlyList<CheckedConcept> Results { get; set; } = [];
}
