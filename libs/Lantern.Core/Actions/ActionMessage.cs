using Lantern.Core.Constants;

namespace Lantern.Core.Actions;

/// <summary>
/// What goes on the queue: the action's id, its type and the type's JSON payload. <paramref name="TraceParent"/> is the
/// W3C trace context of the request that recorded it, so a resend can link back to that request's trace.
/// </summary>
public sealed record ActionMessage(string Id, ActionType Type, string Payload, string TraceParent = null);
