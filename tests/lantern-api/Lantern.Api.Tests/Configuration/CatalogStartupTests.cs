using Lantern.Api.Tests.Infrastructure;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Tests.Configuration;

[Collection(ApiCollection.Name)]
public sealed class CatalogStartupTests(AzuriteFixture azurite) : IDisposable
{
    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    [Theory]
    [InlineData("Catalog:NcertContainer")]
    [InlineData("Catalog:SscContainer")]
    public void Start_WithoutACatalogContainer_Stops(string setting)
    {
        using var broken = factory.WithWebHostBuilder(builder => builder.UseSetting(setting, string.Empty));

        Assert.Throws<OptionsValidationException>(() => broken.CreateClient());
    }

    public void Dispose() => factory.Dispose();
}
