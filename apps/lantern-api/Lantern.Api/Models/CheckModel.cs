namespace Lantern.Api.Models;

public sealed class CheckModel
{
    public Guid CheckId { get; set; }

    public int CheckNumber { get; set; }

    public DateTimeOffset TakenAt { get; set; }

    public int Right { get; set; }

    public int Of { get; set; }

    public IReadOnlyList<WrongConceptModel> Wrong { get; set; } = [];
}
