using Lantern.Core.Actions;
using Lantern.Core.Constants;

namespace Lantern.Core.Repository;

public interface IActionLedger
{
    /// <summary>Records the action. Repeating an id changes nothing.</summary>
    Task RecordAsync(ActionMessage message, DateTimeOffset now, CancellationToken cancellationToken);

    Task<bool> IsPendingAsync(ActionType type, string id, string subscriber, CancellationToken cancellationToken);

    Task MarkSentAsync(ActionType type, string id, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<ActionMessage>> ResendableAsync(
        DateTimeOffset unconfirmedBefore,
        DateTimeOffset confirmedBefore,
        CancellationToken cancellationToken
    );

    Task MarkDoneAsync(ActionType type, string id, string subscriber, CancellationToken cancellationToken);

    Task ExpireDoneAsync(DateTimeOffset recordedBefore, CancellationToken cancellationToken);
}
