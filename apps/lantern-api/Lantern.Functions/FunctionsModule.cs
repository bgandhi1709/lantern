using Lantern.Functions.Handler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lantern.Functions;

public static class FunctionsModule
{
    /// <summary>The dispatcher and one handler per action type. Add a handler here with its <c>ActionType</c>.</summary>
    public static IServiceCollection AddLanternFunctions(this IServiceCollection services)
    {
        services.TryAddScoped<IActionDispatcher, ActionDispatcher>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IActionHandler, RemoveWorkspaceHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IActionHandler, CreateWorkspaceHandler>());

        return services;
    }
}
