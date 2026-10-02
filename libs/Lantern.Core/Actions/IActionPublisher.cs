namespace Lantern.Core.Actions;

public interface IActionPublisher
{
    /// <summary>Writes the action to the ledger, the commit point. Repeating an id changes nothing.</summary>
    Task<ActionMessage> RecordAsync<TPayload>(ActionType type, string id, TPayload payload, CancellationToken cancellationToken);

    /// <summary>
    /// Puts a recorded action on the queue. A failed send is logged, not thrown: the caller's request has already
    /// succeeded, and the ledger lets the message be sent again.
    /// </summary>
    Task SendAsync(ActionMessage message, CancellationToken cancellationToken);
}
