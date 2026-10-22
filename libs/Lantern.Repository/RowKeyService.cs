using System.Globalization;

namespace Lantern.Repository;

internal sealed class RowKeyService : IRowKeyService
{
    private const string SummaryKind = "s_";
    private const string AttemptKind = "a_";
    private const string CheckKind = "c_";

    public string FamilyRowKey => "family";

    public string ProfileRowKey => "profile";

    public string ChildRowPrefix => "child_";

    public string MembershipRowPrefix => "parent_";

    public string FamilyPartition(Guid familyId) => familyId.ToString("D", CultureInfo.InvariantCulture);

    public string ChildRowKey(Guid childId) => ChildRowPrefix + childId.ToString("N", CultureInfo.InvariantCulture);

    public string MembershipRowKey(string uidHash) => MembershipRowPrefix + uidHash;

    public IReadOnlyList<string> ProgressRowPrefixes => [SummaryKind, AttemptKind, CheckKind];

    public IReadOnlyList<string> ProgressRowPrefixesOf(Guid childId) =>
        [.. ProgressRowPrefixes.Select(kind => string.Create(CultureInfo.InvariantCulture, $"{kind}{childId:N}_"))];

    public string ChapterProgressRowPrefix(Guid childId, int classLevel) => $"{SummaryKind}{Scope(childId, classLevel)}_";

    public string ChapterProgressRowKey(Guid childId, int classLevel, string chapterId) =>
        ChapterProgressRowPrefix(childId, classLevel) + chapterId;

    public string AttemptRowPrefix(Guid childId, int classLevel, string chapterId) =>
        string.Create(CultureInfo.InvariantCulture, $"{AttemptKind}{Scope(childId, classLevel)}_{chapterId}_");

    public string AttemptRowKey(Guid childId, int classLevel, string chapterId, int attemptNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"{AttemptRowPrefix(childId, classLevel, chapterId)}{attemptNumber:000}");

    public string CheckRowPrefix(Guid childId, int classLevel, string chapterId) =>
        string.Create(CultureInfo.InvariantCulture, $"{CheckKind}{Scope(childId, classLevel)}_{chapterId}_");

    public string CheckRowKey(Guid childId, int classLevel, string chapterId, int attemptNumber, Guid checkId) =>
        string.Create(CultureInfo.InvariantCulture, $"{CheckRowPrefix(childId, classLevel, chapterId)}{attemptNumber:000}_{checkId:N}");

    private static string Scope(Guid childId, int classLevel) =>
        string.Create(CultureInfo.InvariantCulture, $"{childId:N}_{classLevel:00}");
}
