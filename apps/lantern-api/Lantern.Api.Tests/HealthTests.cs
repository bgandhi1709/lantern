using System.Net;
using Lantern.Api.Tests.Infrastructure;

namespace Lantern.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class HealthTests(AzuriteFixture azurite) : IDisposable
{
    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    [Fact]
    public async Task Live_Anonymous_Returns200()
    {
        using var client = this.factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose() => this.factory.Dispose();
}
