namespace Lantern.Core.Exceptions;

public sealed class InvalidRequestException(string message) : Exception(message);
