using System;
using System.Collections.Generic;

namespace Lantern.Api.Models;

public sealed record ParentProfile(
    string PartitionKey,
    Guid FamilyId,
    string NameCipher,
    string EmailCipher,
    // This family's field-encryption key, wrapped by the Key Vault family-field-key. Unwrap it to
    // decrypt NameCipher/EmailCipher/children's ciphers; clearing it makes them unreadable forever.
    string WrappedFieldKey,
    string Region,
    string Language,
    string ConsentVersion,
    DateTimeOffset ConsentAt,
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
    DateTimeOffset CreatedAt
);

public sealed record ParentAggregate(ParentProfile Profile, IReadOnlyList<ChildRecord> Children);

public sealed record FamilyView(
    Guid FamilyId,
    string Name,
    string Email,
    string Region,
    string Language,
    string ConsentVersion,
    DateTimeOffset ConsentAt,
    IReadOnlyList<ChildView> Children
);

public sealed record ChildView(
    Guid ChildId,
    string Name,
    string? School,
    int ClassLevel,
    int BirthYear
);
