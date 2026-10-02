using System.Globalization;

namespace Lantern.Repository;

internal sealed class RowKeyService : IRowKeyService
{
    public string FamilyRowKey => "family";

    public string ProfileRowKey => "profile";

    public string ChildRowPrefix => "child_";

    public string FamilyPartition(Guid familyId) => familyId.ToString("D", CultureInfo.InvariantCulture);

    public string ChildRowKey(Guid childId) => ChildRowPrefix + childId.ToString("N", CultureInfo.InvariantCulture);

    public string MembershipRowKey(string uidHash) => "parent_" + uidHash;
}
