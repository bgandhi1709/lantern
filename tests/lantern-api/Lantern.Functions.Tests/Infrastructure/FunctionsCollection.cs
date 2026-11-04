using Lantern.Api.Tests.Infrastructure;

namespace Lantern.Functions.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class FunctionsCollection : ICollectionFixture<AzuriteFixture>
{
    public const string Name = "functions";
}
