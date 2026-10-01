using System.Net;
using Lantern.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lantern.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public sealed class LocalDevelopmentTests(AzuriteFixture azurite) : IDisposable
{
    private const string EmulatorHost = "http://auth-emulator:9099";

    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    public void Start_WithTheAuthEmulatorOutsideDevelopment_Refuses(string environment)
    {
        using var configured = Configure(environment, ("Firebase:EmulatorHost", EmulatorHost));

        Assert.Throws<InvalidOperationException>(() => configured.CreateClient());
    }

    [Fact]
    public void Start_WithTheLocalKeyOutsideDevelopment_Refuses()
    {
        using var configured = Configure("Production", ("KeyVault:LocalKeyPath", "/keys/kek.pem"));

        Assert.Throws<InvalidOperationException>(() => configured.CreateClient());
    }

    [Fact]
    public async Task Me_UnsignedTokenWithTheEmulatorOnInDevelopment_IsAuthenticated()
    {
        using var configured = Configure("Development", ("Firebase:EmulatorHost", EmulatorHost));
        using var client = configured.CreateClient().WithBearer(TestTokens.WithoutSignature("dev-parent-1"));

        var response = await client.GetAsync(ApiClientExtensions.Me);

        // 404 not-registered, not 401: the caller was let in and simply has no profile.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Me_UnsignedTokenWithoutTheEmulator_IsRefused()
    {
        using var client = factory.CreateClient().WithBearer(TestTokens.WithoutSignature("dev-parent-1"));

        var response = await client.GetAsync(ApiClientExtensions.Me);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> Configure(string environment, params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    public void Dispose() => factory.Dispose();
}
