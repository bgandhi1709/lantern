namespace Lantern.Api.Tests.Infrastructure;

/// <summary>
/// Shares one Azurite table process across every test class in the collection, so each class does
/// not pay to start and stop its own emulator.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<AzuriteFixture>
{
    public const string Name = "api";
}
