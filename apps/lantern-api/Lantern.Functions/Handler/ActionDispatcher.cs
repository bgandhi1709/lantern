using Lantern.Core.Actions;
using Lantern.Core.Constants;

namespace Lantern.Functions.Handler;

internal sealed class ActionDispatcher(IEnumerable<IActionHandler> handlers) : IActionDispatcher
{
    private readonly Dictionary<ActionType, IActionHandler> _byType = handlers.ToDictionary(handler => handler.Type);

    public Task DispatchAsync(ActionMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        return _byType.TryGetValue(message.Type, out var handler)
            ? handler.HandleAsync(message, cancellationToken)
            : throw new UnknownActionException(message.Type);
    }
}
