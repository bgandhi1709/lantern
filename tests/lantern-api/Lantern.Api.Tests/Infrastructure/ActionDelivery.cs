

extern alias Functions;
using Functions::Lantern.Functions;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Service;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Api.Tests.Infrastructure;

public static class ActionDelivery
{
    /// <summary>Hands an action to the function bound to its key, as the broker does.</summary>
    public static async Task DeliverAsync(this IServiceProvider host, ActionMessage action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = host.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var body = provider.GetRequiredService<IJsonService>().Serialize(action);

        await (
            action.Type switch
            {
                ActionType.RemoveWorkspace => provider.GetRequiredService<RemoveWorkspaceFunction>().RunAsync(body, cancellationToken),
                ActionType.CreateWorkspace => provider.GetRequiredService<CreateWorkspaceFunction>().RunAsync(body, cancellationToken),
                ActionType.RemoveFamily => provider.GetRequiredService<RemoveFamilyFunction>().RunAsync(body, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(action)),
            }
        );
    }
}
