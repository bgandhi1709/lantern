using Lantern.Core.Actions;

namespace Lantern.Functions.Handler;

public interface IActionDispatcher
{
    /// <exception cref="UnknownActionException">No handler is registered for the message's type.</exception>
    Task DispatchAsync(ActionMessage message, CancellationToken cancellationToken);
}
