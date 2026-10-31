using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lantern.Functions;

public static class FunctionsModule
{
    /// <summary>One Function per subscriber. A new subscriber is a Function here, a queue binding and a setting.</summary>
    public static IServiceCollection AddLanternFunctions(this IServiceCollection services)
    {
        services.TryAddScoped<RemoveWorkspaceFunction>();
        services.TryAddScoped<CreateWorkspaceFunction>();
        services.TryAddScoped<RemoveFamilyFunction>();

        return services;
    }
}
