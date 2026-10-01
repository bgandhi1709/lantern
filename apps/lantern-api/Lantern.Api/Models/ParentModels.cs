using System;
using System.Collections.Generic;

namespace Lantern.Api.Models;

public sealed record ParentProfile(
    string PartitionKey,
    Guid ParentId,
    Guid FamilyId,
    string NameCipher,
    string EmailCipher,
    string Language,
    string ConsentVersion,
    DateTimeOffset ConsentAt,
    DateTimeOffset CreatedAt
);

public sealed record FamilyRecord(
    Guid FamilyId,
    string Region,
    // Unwrap to decrypt the parents' and children's ciphers; clearing it makes them unreadable forever.
    string WrappedFieldKey,
    KeyScheme KeyScheme,
    DateTimeOffset CreatedAt
);

public sealed record ChildRecord(
    Guid ChildId,
    string NameCipher,
    string? SchoolCipher,
    int ClassLevel,
    int BirthYear,
    // Children saved together share a timestamp; Position keeps the order they were entered in.
    int Position,
    DateTimeOffset CreatedAt,
    ChildStatus Status = ChildStatus.Active
);

public sealed record FamilyAggregate(
    ParentProfile Parent,
    FamilyRecord Family,
    // Stamp of the family row when read; an add commits only if nobody else changed the Family since.
    string FamilyETag,
    IReadOnlyList<ChildRecord> Children
);

public sealed record FamilyView(
    Guid FamilyId,
    string Region,
    ParentView Parent,
    IReadOnlyList<ChildView> Children
);

public sealed record ParentView(
    Guid ParentId,
    string Name,
    string Email,
    string Language,
    string ConsentVersion,
    DateTimeOffset ConsentAt
);

public sealed record ChildView(
    Guid ChildId,
    string Name,
    string? School,
    int ClassLevel,
    int BirthYear
);

public enum KeyScheme
{
    KeyVault,
}

public enum ChildStatus
{
    Active,
    Deleting,
}
