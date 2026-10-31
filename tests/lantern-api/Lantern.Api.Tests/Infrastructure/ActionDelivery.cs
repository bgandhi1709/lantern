using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Service;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Api.Tests.Infrastructure;

extern alias Functions;

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
                ActionType.RemoveWorkspace => provider.GetRequiredService<Functions::Lantern.Functions.RemoveWorkspaceFunction>().RunAsync(body, cancellationToken),
                ActionType.CreateWorkspace => provider.GetRequiredService<Functions::Lantern.Functions.CreateWorkspaceFunction>().RunAsync(body, cancellationToken),
                ActionType.RemoveFamily => provider.GetRequiredService<Functions::Lantern.Functions.RemoveFamilyFunction>().RunAsync(body, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(action)),
            }
        );
    }
}
