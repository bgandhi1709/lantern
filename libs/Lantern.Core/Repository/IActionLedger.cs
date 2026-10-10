using Lantern.Core.Actions;
using Lantern.Core.Constants;

namespace Lantern.Core.Repository;

/// <summary>
/// The record of every action the API accepted and no handler has finished. A row is written before its message is
/// sent, so a lost message can always be sent again; the handler removes the row last.
/// </summary>
public interface IActionLedger
{
    /// <summary>Records the action. Repeating an id changes nothing.</summary>
    Task RecordAsync(ActionMessage message, DateTimeOffset now, CancellationToken cancellationToken);

    Task<bool> IsPendingAsync(Constants.ActionType type, string id, CancellationToken cancellationToken);

    Task MarkSentAsync(ActionType type, string id, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Actions whose message was last sent at or before the cutoff.</summary>
    Task<IReadOnlyList<ActionMessage>> UnsentSinceAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>The last step of a handler. Safe to repeat.</summary>
    Task CompleteAsync(Constants.ActionType type, string id, CancellationToken cancellationToken);
}
