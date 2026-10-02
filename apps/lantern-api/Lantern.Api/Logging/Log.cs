namespace Lantern.Api.Logging;

internal static partial class Log
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Rejected an ID token: {Reason}")]
    internal static partial void TokenInvalid(ILogger logger, string reason);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Registration refused: this account is already registered")]
    internal static partial void AlreadyRegistered(ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Registration refused: {Reason}")]
    internal static partial void RegistrationInvalid(ILogger logger, string reason);

    [LoggerMessage(
        EventId = 1013,
        Level = LogLevel.Warning,
        Message = "Sending actions again at start-up failed ({ErrorType}); it will be tried at the next start"
    )]
    internal static partial void ActionResendFailed(ILogger logger, string errorType);
}
