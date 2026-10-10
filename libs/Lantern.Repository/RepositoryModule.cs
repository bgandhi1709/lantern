using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Lantern.Core.Configuration;
using Lantern.Core.Repository;
using Lantern.Repository.Entities;
using Lantern.Repository.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Lantern.Repository;

public static class RepositoryModule
{
    /// <summary>The storage account, a unit of work per entity, the repositories and the stores.</summary>
    public static IServiceCollection AddLanternRepository(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(serviceProvider =>
        {
            var options = Storage(serviceProvider);

            return string.IsNullOrWhiteSpace(options.ConnectionString)
                ? new TableServiceClient(new Uri(options.TableEndpoint), new DefaultAzureCredential())
                : new TableServiceClient(options.ConnectionString);
        });
        services.TryAddSingleton(serviceProvider =>
        {
            var options = Storage(serviceProvider);

            return string.IsNullOrWhiteSpace(options.ConnectionString)
                ? new BlobServiceClient(new Uri(options.BlobEndpoint), new DefaultAzureCredential())
                : new BlobServiceClient(options.ConnectionString);
        });

        AddUnitOfWork<FamilyEntity>(services, options => options.FamiliesTable);
        AddUnitOfWork<ChildEntity>(services, options => options.FamiliesTable);
        AddUnitOfWork<ParentEntity>(services, options => options.ParentsTable);
        AddUnitOfWork<ActionEntity>(services, options => options.ActionsTable);

        services.TryAddSingleton<IRowKeyService, RowKeyService>();
        services.TryAddScoped<IFamilyRepository, FamilyRepository>();
        services.TryAddScoped<IChildRepository, ChildRepository>();
        services.TryAddScoped<IRepositoryBase<Core.Models.Child>>(serviceProvider => serviceProvider.GetRequiredService<IChildRepository>());
        services.TryAddScoped<IRepositoryBase<Core.Models.Family>>(serviceProvider => serviceProvider.GetRequiredService<IFamilyRepository>());
        services.TryAddSingleton<IActionLedger, ActionLedger>();
        services.TryAddSingleton<IWorkspaceStore>(serviceProvider => new WorkspaceStore(
            serviceProvider.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(Storage(serviceProvider).WorkspaceContainer),
            createContainer: !string.IsNullOrWhiteSpace(Storage(serviceProvider).ConnectionString)
        ));

        return services;
    }

    private static StorageOptions Storage(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;

    private static void AddUnitOfWork<TEntity>(IServiceCollection services, Func<StorageOptions, string> table)
        where TEntity : class, ITableEntity, new() =>
        services.TryAddSingleton<IUnitOfWork<TEntity>>(serviceProvider =>
        {
            var options = Storage(serviceProvider);

            return new UnitOfWork<TEntity>(
                serviceProvider.GetRequiredService<TableServiceClient>().GetTableClient(table(options)),
                createTable: !string.IsNullOrWhiteSpace(options.ConnectionString)
            );
        });
}
