using Microsoft.Extensions.Logging;

namespace Lantern.Base.Logging;

// Only ids and counts are logged, never a name or any text a Parent entered.
internal static partial class BaseLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Registered family {FamilyId} with {ChildCount} child(ren)")]
    internal static partial void FamilyRegistered(ILogger logger, Guid familyId, int childCount);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Delete requested for family {FamilyId}")]
    internal static partial void FamilyDeleteRequested(ILogger logger, Guid familyId);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Added child {ChildId} to family {FamilyId}")]
    internal static partial void ChildAdded(ILogger logger, Guid familyId, Guid childId);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "Edited child {ChildId} of family {FamilyId}")]
    internal static partial void ChildEdited(ILogger logger, Guid familyId, Guid childId);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Information, Message = "Delete requested for child {ChildId} of family {FamilyId}")]
    internal static partial void ChildDeleteRequested(ILogger logger, Guid familyId, Guid childId);
}
