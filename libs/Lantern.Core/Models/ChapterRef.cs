namespace Lantern.Core.Models;

public sealed class ChapterRef(Guid familyId, Guid childId, int classLevel, string chapterId)
{
    public Guid FamilyId { get; } = familyId;

    public Guid ChildId { get; } = childId;

    public int ClassLevel { get; } = classLevel;

    public string ChapterId { get; } = chapterId;
}
