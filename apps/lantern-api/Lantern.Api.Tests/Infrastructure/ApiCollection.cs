namespace Lantern.Api.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<AzuriteFixture>
{
    public const string Name = "api";
}
