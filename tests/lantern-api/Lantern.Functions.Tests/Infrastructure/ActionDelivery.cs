using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Service;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Functions.Tests.Infrastructure;

public static class ActionDelivery
{
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
