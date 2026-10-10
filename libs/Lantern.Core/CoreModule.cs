using Lantern.Core.Actions;
using Lantern.Core.Configuration;
using Lantern.Core.Security;
using Lantern.Core.Service;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lantern.Core;

public static class CoreModule
{
    /// <summary>
    /// The uid hash, the action publisher, the generic service and one Mapster mapper built from every layer's
    /// <see cref="IRegister"/>. Options are bound here; a host that uses them adds <c>ValidateOnStart</c>.
    /// </summary>
    public static IServiceCollection AddLanternCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName)).ValidateDataAnnotations();
        services.AddOptions<ServiceBusOptions>().Bind(configuration.GetSection(ServiceBusOptions.SectionName)).ValidateDataAnnotations();
        services.AddOptions<ActionOptions>().Bind(configuration.GetSection(ActionOptions.SectionName)).ValidateDataAnnotations();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICryptoService, CryptoService>();
        services.TryAddSingleton<IServiceBusService, ServiceBusService>();
        services.TryAddScoped(typeof(IServiceBase<>), typeof(ServiceBase<>));
        services.TryAddSingleton<IActionPublisher, ActionPublisher>();

        services.TryAddSingleton(serviceProvider =>
        {
            var config = new TypeAdapterConfig();
            config.Apply(serviceProvider.GetServices<IRegister>());
            return config;
        });
        services.TryAddSingleton<IMapper>(serviceProvider => new Mapper(serviceProvider.GetRequiredService<TypeAdapterConfig>()));

        return services;
    }
}
