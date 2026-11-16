namespace Lantern.Api.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<AzuriteFixture>, ICollectionFixture<CosmosFixture>
{
    public const string Name = "api";
}
