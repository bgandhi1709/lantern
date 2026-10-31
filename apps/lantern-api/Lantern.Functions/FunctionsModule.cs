using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lantern.Functions;

public static class FunctionsModule
{
    public static IServiceCollection AddLanternFunctions(this IServiceCollection services)
    {
        services.TryAddScoped<RemoveWorkspaceFunction>();
        services.TryAddScoped<CreateWorkspaceFunction>();
        services.TryAddScoped<RemoveFamilyFunction>();

        return services;
    }
}
