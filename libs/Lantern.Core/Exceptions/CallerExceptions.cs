namespace Lantern.Core.Exceptions;

// Only these reach a caller as a problem response; anything else is a real 500.

public sealed class AlreadyRegisteredException() : Exception("This account is already registered.");

public sealed class NotRegisteredException() : Exception("This account is not registered.");

public sealed class InvalidRequestException(string message) : Exception(message);

public sealed class NotFoundException(string resource) : Exception($"No such {resource} in this family.")
{
    public string Resource { get; } = resource;
}

public sealed class ChildLimitReachedException() : Exception("A family holds at most six children.");

public sealed class ChildDeletingException() : Exception("This child is being deleted.");

public sealed class FamilyChangedException() : Exception("The family changed; try again.");

public sealed class CallerNotIdentifiedException() : Exception("The token names no caller.");
