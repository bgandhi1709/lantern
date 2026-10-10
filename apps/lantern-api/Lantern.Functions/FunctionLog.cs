using Lantern.Core.Constants;
using Microsoft.Extensions.Logging;

namespace Lantern.Functions;

// Only ids and error types are logged, never a name or any text a Parent entered.
internal static partial class FunctionLog
{
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Warning,
        Message = "The {ActionType} action {ActionId} failed on delivery {Delivery} ({ErrorType}); the queue will deliver it again"
    )]
    internal static partial void ActionFailed(ILogger logger, ActionType actionType, string actionId, int delivery, string errorType);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Message {MessageId} was dead-lettered: {Reason}")]
    internal static partial void ActionRejected(ILogger logger, string messageId, string reason);

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Removed the workspace of child {ChildId} of family {FamilyId}")]
    internal static partial void WorkspaceRemoved(ILogger logger, Guid familyId, Guid childId);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Information, Message = "Created the class {ClassLevel} workspace of child {ChildId} of family {FamilyId}")]
    internal static partial void WorkspaceCreated(ILogger logger, Guid familyId, Guid childId, int classLevel);

    [LoggerMessage(EventId = 2020, Level = LogLevel.Information, Message = "Erased family {FamilyId}")]
    internal static partial void FamilyErased(ILogger logger, Guid familyId);
}
