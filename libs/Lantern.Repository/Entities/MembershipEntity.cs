namespace Lantern.Repository.Entities;

// families table, partition {familyId}, row "parent_{uidHash}": one per Parent of the Family.
internal sealed class MembershipEntity : TableEntityBase
{
    public Guid ParentId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
