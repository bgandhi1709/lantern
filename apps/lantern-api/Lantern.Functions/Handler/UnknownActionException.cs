using Lantern.Core.Constants;

namespace Lantern.Functions.Handler;

public sealed class UnknownActionException(ActionType type) : Exception($"No handler for action type '{type}'.");
