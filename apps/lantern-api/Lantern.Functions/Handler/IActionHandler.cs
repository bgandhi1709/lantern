using Lantern.Core.Actions;

namespace Lantern.Functions.Handler;

/// <summary>Handles one action type. Every step must be safe to repeat: a message can arrive twice.</summary>
public interface IActionHandler
{
    ActionType Type { get; }

    Task HandleAsync(ActionMessage message, CancellationToken cancellationToken);
}
