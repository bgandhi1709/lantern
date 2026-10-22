namespace Lantern.Repository.Entities;

internal sealed class CheckEntity : TableEntityBase
{
    public Guid ChildId { get; set; }

    public int ClassLevel { get; set; }

    public string ChapterId { get; set; }

    public int AttemptNumber { get; set; }

    public Guid CheckId { get; set; }

    public int CheckNumber { get; set; }

    public DateTimeOffset TakenAt { get; set; }

    public int Right { get; set; }

    public int Of { get; set; }

    public string Results { get; set; }

    public string ContentVersion { get; set; }
}
