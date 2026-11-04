namespace Lantern.Repository.Entities;

internal sealed class ActionEntity : TableEntityBase
{
    public string Payload { get; set; }

    public string TraceParent { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public DateTimeOffset LastSentAt { get; set; }

    public string DoneBy { get; set; } = string.Empty;
}
