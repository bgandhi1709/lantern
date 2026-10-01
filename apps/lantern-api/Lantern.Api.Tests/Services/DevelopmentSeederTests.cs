using System.Net.Http.Json;
using Lantern.Api.Contracts;
using Lantern.Api.Services;
using Lantern.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Api.Tests.Services;

[Collection(ApiCollection.Name)]
public sealed class DevelopmentSeederTests(AzuriteFixture azurite) : IDisposable
{
    // Fixed uids mean a Family from an earlier test would be wrapped by another test's key, so each test gets its own tables.
    private readonly WebApplicationFactory<Program> factory = new LanternApiFactory(azurite.ConnectionString).WithWebHostBuilder(
        builder =>
        {
            builder.UseSetting("Storage:ParentsTable", $"parents{Guid.NewGuid():N}");
            builder.UseSetting("Storage:FamiliesTable", $"families{Guid.NewGuid():N}");
        }
    );

    [Fact]
    public async Task Seed_CreatesOneFamilyWithTenChildrenInTenClassesForBothParents()
    {
        await SeedAsync();

        var one = await MeAsync(DevelopmentSeeder.ParentOneUid);
        var two = await MeAsync(DevelopmentSeeder.ParentTwoUid);

        Assert.Equal(one.FamilyId, two.FamilyId);
        Assert.NotEqual(one.Parent.ParentId, two.Parent.ParentId);
        Assert.Equal(Enumerable.Range(1, 10), one.Children.Select(child => child.ClassLevel));
        Assert.Equal(one.Children.Select(child => child.ChildId), two.Children.Select(child => child.ChildId));
        Assert.All(one.Children, child => Assert.False(string.IsNullOrWhiteSpace(child.Name)));
    }

    [Fact]
    public async Task Seed_StartsAClassSpaceForEveryChild()
    {
        await SeedAsync();
        var family = await MeAsync(DevelopmentSeeder.ParentOneUid);

        var container = azurite.CreateBlobClient().GetBlobContainerClient("family");
        foreach (var child in family.Children)
        {
            Assert.True(await container.GetBlobClient($"{family.FamilyId:D}/{child.ChildId:D}/{child.ClassLevel}/class.json").ExistsAsync());
        }
    }

    [Fact]
    public async Task Seed_Twice_ChangesNothing()
    {
        await SeedAsync();
        var first = await MeAsync(DevelopmentSeeder.ParentOneUid);

        await SeedAsync();

        var again = await MeAsync(DevelopmentSeeder.ParentOneUid);
        Assert.Equal(first.FamilyId, again.FamilyId);
        Assert.Equal(10, again.Children.Count);
    }

    [Fact]
    public async Task OtherCallers_CannotSeeTheSeededFamily()
    {
        await SeedAsync();
        using var stranger = factory.CreateClient().WithBearer(TestTokens.Create($"uid-{Guid.NewGuid():N}"));

        var response = await stranger.GetAsync(ApiClientExtensions.Me);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync(CancellationToken.None);
    }

    private async Task<FamilyResponse> MeAsync(string uid)
    {
        using var client = factory.CreateClient().WithBearer(TestTokens.Create(uid));

        return (await client.GetFromJsonAsync<FamilyResponse>(ApiClientExtensions.Me))!;
    }

    public void Dispose() => factory.Dispose();
}
