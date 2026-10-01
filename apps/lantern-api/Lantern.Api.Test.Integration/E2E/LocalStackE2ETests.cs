using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Storage.Blobs;
using Lantern.Api.Contracts;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Lantern.Api.Test.Integration.E2E;

// Needs the local Docker stack (deploy/local): the seeded dev Family, the Auth Emulator and Azurite.
// Run with E2E_BASE_URL and E2E_AUTH_EMULATOR set, as CI's e2e-docker job does.
[Trait("Category", "E2E")]
public sealed class LocalStackE2ETests
{
    private const string ProjectId = "lantern-ai-bg1709";
    private static readonly Uri MeUri = new("/v1/me", UriKind.Relative);

    // Azurite's published development account; not a secret.
    private const string AzuriteBlobs =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
        + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
        + "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    [Fact]
    public async Task SeededFamily_HasTenChildrenInTenClasses()
    {
        var family = await MeAsync("dev-parent-1");

        Assert.Equal(Enumerable.Range(1, 10), family.Children.Select(child => child.ClassLevel));
        Assert.All(family.Children, child => Assert.False(string.IsNullOrWhiteSpace(child.Name)));
    }

    [Fact]
    public async Task SeededFamily_IsSharedByBothParents()
    {
        var one = await MeAsync("dev-parent-1");
        var two = await MeAsync("dev-parent-2");

        Assert.Equal(one.FamilyId, two.FamilyId);
        Assert.NotEqual(one.Parent.ParentId, two.Parent.ParentId);
        Assert.Equal(one.Children.Select(child => child.ChildId), two.Children.Select(child => child.ChildId));
    }

    [Fact]
    public async Task SeededFamily_HasAClassSpaceForEveryChild()
    {
        var family = await MeAsync("dev-parent-1");
        var container = new BlobServiceClient(AzuriteBlobs).GetBlobContainerClient("family");

        foreach (var child in family.Children)
        {
            var marker = container.GetBlobClient($"{family.FamilyId:D}/{child.ChildId:D}/{child.ClassLevel}/class.json");
            Assert.True(await marker.ExistsAsync(), $"Class {child.ClassLevel} has no space");
        }
    }

    [Fact]
    public async Task AStranger_CannotSeeTheSeededFamily()
    {
        var response = await (await ClientAsync($"stranger-{Guid.NewGuid():N}")).GetAsync(MeUri);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_IsRefused()
    {
        using var client = new HttpClient { BaseAddress = BaseUrl };

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MeUri)).StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example", ProjectId, 600)]
    [InlineData($"https://securetoken.google.com/{ProjectId}", "another-project", 600)]
    [InlineData($"https://securetoken.google.com/{ProjectId}", ProjectId, -3600)]
    public async Task UnsignedToken_WithTheWrongIssuerAudienceOrExpiry_IsRefused(string issuer, string audience, int expiresInSeconds)
    {
        var token = new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = audience,
                IssuedAt = DateTime.UtcNow.AddSeconds(expiresInSeconds - 7200),
                NotBefore = DateTime.UtcNow.AddSeconds(expiresInSeconds - 7200),
                Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds),
                Claims = new Dictionary<string, object> { ["sub"] = "dev-parent-1" },
            }
        );
        using var client = new HttpClient { BaseAddress = BaseUrl };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MeUri)).StatusCode);
    }

    private static Uri BaseUrl => new(Require("E2E_BASE_URL"));

    private static async Task<FamilyResponse> MeAsync(string uid)
    {
        var response = await (await ClientAsync(uid)).GetAsync(MeUri);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<FamilyResponse>())!;
    }

    private static async Task<HttpClient> ClientAsync(string uid)
    {
        var client = new HttpClient { BaseAddress = BaseUrl };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await EmulatorTokens.MintAsync(Require("E2E_AUTH_EMULATOR"), uid)
        );

        return client;
    }

    private static string Require(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is required to run the local E2E tests.");
}
