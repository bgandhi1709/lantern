namespace Lantern.Repository;

// Partition and row names. They are also part of each field's encryption context, so they stay fixed in code.
internal interface IRowKeyService
{
    string FamilyRowKey { get; }

    string ProfileRowKey { get; }

    string ChildRowPrefix { get; }

    string FamilyPartition(Guid familyId);

    string ChildRowKey(Guid childId);

    string MembershipRowKey(string uidHash);
}
