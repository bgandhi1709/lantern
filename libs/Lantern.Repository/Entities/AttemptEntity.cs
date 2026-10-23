namespace Lantern.Repository.Entities;

internal sealed class AttemptEntity : TableEntityBase
{
    public Guid ChildId { get; set; }

    public int ClassLevel { get; set; }

    public string ChapterId { get; set; }

    public int AttemptNumber { get; set; }

    public Guid PlanId { get; set; }

    public DateTimeOffset PlannedAt { get; set; }

    public int ConceptCount { get; set; }

    public int ConceptsDone { get; set; }

    public string DoneConcepts { get; set; } = "{}";

    public string ContentVersion { get; set; }
}
