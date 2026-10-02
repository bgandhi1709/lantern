using Lantern.Base.Services;
using Lantern.Base.Validation;
using Lantern.Core;
using Lantern.Core.Models;
using Lantern.Core.Service;
using Lantern.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lantern.Base;

public static class BaseModule
{
    /// <summary>Every layer below the host: Core, Repository, and the services and validators.</summary>
    public static IServiceCollection AddLanternBase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLanternCore(configuration).AddLanternRepository(configuration);

        services.TryAddSingleton<IValidator<Child>, ChildValidator>();
        services.TryAddSingleton<IValidator<Registration>, RegistrationValidator>();
        services.TryAddSingleton<IChildTextNormalizer, ChildTextNormalizer>();
        services.TryAddScoped<IFamilyService, FamilyService>();
        services.TryAddScoped<IChildService, ChildService>();

        return services;
    }
}
