using Lantern.Core.Actions;
using Microsoft.Extensions.Logging;

namespace Lantern.Core.Logging;

// Only ids and error types are logged, never a name or any text a Parent entered.
internal static partial class CoreLog
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Sending the {ActionType} action failed ({ErrorType}); it will be sent again when the API next starts"
    )]
    internal static partial void ActionPublishFailed(ILogger logger, ActionType actionType, string errorType);
}
