namespace Lantern.Repository.Entities;

// actions table, partition {ActionType}, row {action id}: one per action no handler has finished.
internal sealed class ActionEntity : TableEntityBase
{
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset LastSentAt { get; set; }
}
