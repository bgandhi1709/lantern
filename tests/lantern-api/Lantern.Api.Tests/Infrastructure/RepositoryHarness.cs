using Azure.Data.Tables;
using Lantern.Core.Configuration;
using Lantern.Core.Security;
using Lantern.Repository;
using Lantern.Repository.Entities;
using Lantern.Repository.UnitOfWork;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Tests.Infrastructure;

// The real repositories over fresh Azurite tables.
internal sealed class RepositoryHarness : IDisposable
{
    private readonly IMapper mapper;
    private readonly UnitOfWork<FamilyEntity> familyRows;
    private readonly UnitOfWork<ChildEntity> childRows;
    private readonly UnitOfWork<ParentEntity> parentRows;

    public RepositoryHarness(string connectionString, bool createTables = true, string parentsTable = null, string familiesTable = null)
    {
        var service = new TableServiceClient(connectionString);
        Parents = service.GetTableClient(parentsTable ?? $"parents{Guid.NewGuid():N}");
        Families = service.GetTableClient(familiesTable ?? $"families{Guid.NewGuid():N}");
        Crypto = new CryptoService(Options.Create(new SecurityOptions { Key = LanternApiFactory.SecurityKey }));

        var config = new TypeAdapterConfig();
        config.Apply(new RepositoryProfile());
        mapper = new Mapper(config);

        familyRows = new UnitOfWork<FamilyEntity>(Families, createTables);
        childRows = new UnitOfWork<ChildEntity>(Families, createTables);
        parentRows = new UnitOfWork<ParentEntity>(Parents, createTables);
    }

    public static RowKeyService KeyService { get; } = new();

    public TableClient Parents { get; }

    public TableClient Families { get; }

    public CryptoService Crypto { get; }

    public FamilyRepository FamilyRepository() =>
        new(familyRows, parentRows, Crypto, mapper, KeyService);

    public ChildRepository ChildRepository() =>
        new(childRows, familyRows, mapper, KeyService);

    public void Dispose()
    {
    }
}
