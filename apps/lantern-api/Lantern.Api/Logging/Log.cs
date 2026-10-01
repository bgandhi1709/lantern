namespace Lantern.Api.Logging;

internal static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Registered family {FamilyId} with {ChildCount} child(ren)"
    )]
    internal static partial void FamilyRegistered(ILogger logger, Guid familyId, int childCount);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Rejected an ID token: {Reason}"
    )]
    internal static partial void TokenInvalid(ILogger logger, string reason);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Registration refused: this account is already registered"
    )]
    internal static partial void AlreadyRegistered(ILogger logger);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Registration refused: {Reason}"
    )]
    internal static partial void RegistrationInvalid(ILogger logger, string reason);


    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Added child {ChildId} to family {FamilyId}")]
    internal static partial void ChildAdded(ILogger logger, Guid familyId, Guid childId);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "Edited child {ChildId} of family {FamilyId}")]
    internal static partial void ChildEdited(ILogger logger, Guid familyId, Guid childId);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Information,
        Message = "Delete requested for child {ChildId} of family {FamilyId}"
    )]
    internal static partial void ChildDeleteRequested(ILogger logger, Guid familyId, Guid childId);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Information, Message = "Deleted child {ChildId} of family {FamilyId}")]
    internal static partial void ChildDeleted(ILogger logger, Guid familyId, Guid childId);

    [LoggerMessage(
        EventId = 1014,
        Level = LogLevel.Warning,
        Message = "Deleting child {ChildId} of family {FamilyId} failed on attempt {Attempt} ({ErrorType}); it will be retried"
    )]
    internal static partial void ChildDeleteFailed(
        ILogger logger,
        Guid familyId,
        Guid childId,
        int attempt,
        string errorType
    );

    [LoggerMessage(
        EventId = 1015,
        Level = LogLevel.Warning,
        Message = "Looking for child deletes to finish failed ({ErrorType}); trying again next tick"
    )]
    internal static partial void ChildDeleteSweepFailed(ILogger logger, string errorType);
}
