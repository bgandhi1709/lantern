using System.Globalization;

namespace Lantern.Api.Repository;

internal sealed class RowKeys : IRowKeys
{
    private const string ChildRowPrefix = "child_";

    public string FamilyPartition(Guid familyId) => familyId.ToString("D", CultureInfo.InvariantCulture);

    public string ChildRowKey(Guid childId) => ChildRowPrefix + childId.ToString("N", CultureInfo.InvariantCulture);

    public bool IsChildRow(string rowKey)
    {
        ArgumentNullException.ThrowIfNull(rowKey);

        return rowKey.StartsWith(ChildRowPrefix, StringComparison.Ordinal);
    }
}
