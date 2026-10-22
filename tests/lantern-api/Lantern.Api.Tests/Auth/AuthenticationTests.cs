using System.Net;
using System.Text.RegularExpressions;
using Lantern.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
// GET /v1/me is the probe: 404 means signed in but not registered, 401 means the token was refused.
public sealed class AuthenticationTests(AzuriteFixture azurite) : IDisposable
{
    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ApiClientExtensions.Me);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.RegisterAsync(new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_IsAuthenticated()
    {
        using var client = factory.CreateClient().WithBearer(TestTokens.Create(NewUid()));

        var response = await client.GetAsync(ApiClientExtensions.Me);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong-audience")]
    [InlineData("wrong-issuer")]
    [InlineData("expired")]
    [InlineData("signed-by-another-key")]
    [InlineData("garbage")]
    public async Task Me_WithRefusedToken_Returns401(string scenario)
    {
        var uid = NewUid();
        var token = scenario switch
        {
            "wrong-audience" => TestTokens.Create(uid, audience: "another-project"),
            "wrong-issuer" => TestTokens.Create(uid, issuer: "https://securetoken.google.com/another-project"),
            "expired" => TestTokens.Create(uid, expires: DateTimeOffset.UtcNow.AddHours(-2)),
            "signed-by-another-key" => TestTokens.Create(uid, key: TestTokens.OtherKey),
            _ => "not.a.jwt",
        };
        using var client = factory.CreateClient().WithBearer(token);

        var response = await client.GetAsync(ApiClientExtensions.Me);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("error", response.Headers.WwwAuthenticate.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EveryControllerEndpoint_WithoutToken_Returns401()
    {
        var endpoints = factory
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .ToList();
        Assert.NotEmpty(endpoints);
        using var client = factory.CreateClient();

        foreach (var endpoint in endpoints)
        {
            var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods[0];
            var path = "/" + Regex.Replace(endpoint.RoutePattern.RawText!, @"\{[^}]+\}", Guid.NewGuid().ToString());

            var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{method} {endpoint.RoutePattern.RawText} returned {response.StatusCode}");
        }
    }

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    public void Dispose() => factory.Dispose();
}
