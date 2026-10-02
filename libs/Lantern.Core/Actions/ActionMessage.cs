namespace Lantern.Core.Actions;

/// <summary>What goes on the queue: the action's id, its type and the type's JSON payload.</summary>
public sealed record ActionMessage(string Id, ActionType Type, string Payload);
