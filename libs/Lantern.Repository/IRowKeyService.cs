namespace Lantern.Repository;

// Partition and row names. Once released they stay fixed in code.
internal interface IRowKeyService
{
    string FamilyRowKey { get; }

    string ProfileRowKey { get; }

    string ChildRowPrefix { get; }

    string MembershipRowPrefix { get; }

    string FamilyPartition(Guid familyId);

    string ChildRowKey(Guid childId);

    string MembershipRowKey(string uidHash);

    IReadOnlyList<string> ProgressRowPrefixes { get; }

    IReadOnlyList<string> ProgressRowPrefixesOf(Guid childId);

    string ChapterProgressRowPrefix(Guid childId, int classLevel);

    string ChapterProgressRowKey(Guid childId, int classLevel, string chapterId);

    string AttemptRowPrefix(Guid childId, int classLevel, string chapterId);

    string AttemptRowKey(Guid childId, int classLevel, string chapterId, int attemptNumber);

    string CheckRowPrefix(Guid childId, int classLevel, string chapterId);

    string CheckRowKey(Guid childId, int classLevel, string chapterId, int attemptNumber, Guid checkId);
}
