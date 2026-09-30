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
}
