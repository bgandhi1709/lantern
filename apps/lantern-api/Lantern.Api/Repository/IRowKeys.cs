namespace Lantern.Api.Repository;

// Table partition and row names. They are also part of each field's encryption context, so they stay fixed in code.
public interface IRowKeys
{
    string FamilyPartition(Guid familyId);

    string ChildRowKey(Guid childId);

    bool IsChildRow(string rowKey);
}
