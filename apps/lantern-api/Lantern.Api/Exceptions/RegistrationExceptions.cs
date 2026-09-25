namespace Lantern.Api.Exceptions;

public sealed class AlreadyRegisteredException() : Exception("This account is already registered.");

public sealed class NotRegisteredException() : Exception("This account is not registered.");

public sealed class InvalidRegistrationException(string message) : Exception(message);
