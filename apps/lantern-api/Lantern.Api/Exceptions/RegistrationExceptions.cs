namespace Lantern.Api.Exceptions;

public sealed class AlreadyRegisteredException() : Exception("This account is already registered.");

public sealed class NotRegisteredException() : Exception("This account is not registered.");

public sealed class InvalidRegistrationException(string message) : Exception(message);

public sealed class ChildNotFoundException() : Exception("No such child in this family.");

public sealed class ChildLimitReachedException() : Exception("A family holds at most six children.");

public sealed class ChildDeletingException() : Exception("This child is being deleted.");

public sealed class FamilyChangedException() : Exception("The family changed; try again.");

public sealed class CallerNotIdentifiedException() : Exception("The token names no caller.");
