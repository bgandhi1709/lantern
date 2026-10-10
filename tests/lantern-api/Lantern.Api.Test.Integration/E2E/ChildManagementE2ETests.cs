using System.Net;
using System.Net.Http.Json;
using Azure.Storage.Blobs;
using Lantern.Api.Models;

namespace Lantern.Api.Test.Integration.E2E;

// Needs the local Docker stack (deploy/local). Delete is asynchronous: the API returns 202 and the worker in the
// container finishes it within its poll interval.
[Trait("Category", "E2E")]
public sealed class ChildManagementE2ETests(RegisteredFamilyFixture fixture) : IClassFixture<RegisteredFamilyFixture>
{
    private static readonly Uri Children = new("/v1/family/children", UriKind.Relative);
    private static readonly Uri Me = new("/v1/me", UriKind.Relative);

    // Azurite's published development account; not a secret.
    private const string AzuriteBlobs =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
        + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
        + "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    [Fact]
    public async Task AddEditAndDeleteAChild_EndToEnd()
    {
        var id = Guid.NewGuid();

        var added = await fixture.Client.PostAsJsonAsync(
            Children,
            new ChildAddModel { ChildId = id, Name = "locked-kavya", ClassLevel = 6, BirthYear = "locked-year", School = "locked-school" }
        );
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var container = new BlobServiceClient(AzuriteBlobs).GetBlobContainerClient("family");
        var marker = container.GetBlobClient($"{fixture.Family.FamilyId:D}/{id:D}/6/class.json");
        Assert.True(await WaitAsync(async () => await marker.ExistsAsync()), "The worker did not start the Workspace in time.");

        var edited = await fixture.Client.PutAsJsonAsync(
            new Uri($"/v1/family/children/{id}", UriKind.Relative),
            new { Name = "locked-kavya-p", School = "locked-school-2", BirthYear = "locked-year" }
        );
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var afterEdit = (await fixture.Client.GetFromJsonAsync<FamilyModel>(Me))!.Children.Single(c => c.ChildId == id);
        Assert.Equal(("locked-kavya-p", "locked-school-2", 6), (afterEdit.Name, afterEdit.School, afterEdit.ClassLevel));

        var deleted = await fixture.Client.DeleteAsync(new Uri($"/v1/family/children/{id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Accepted, deleted.StatusCode);
        Assert.DoesNotContain((await fixture.Client.GetFromJsonAsync<FamilyModel>(Me))!.Children, c => c.ChildId == id);

        Assert.True(await WaitAsync(async () => !await marker.ExistsAsync()), "The worker did not finish the delete in time.");
    }

    private static async Task<bool> WaitAsync(Func<Task<bool>> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline && !await done())
        {
            await Task.Delay(500);
        }

        return await done();
    }

    [Fact]
    public async Task AStranger_CannotDeleteOrEditTheSeededFamilysChild()
    {
        var seeded = await SeededMeAsync("dev-parent-1");
        var target = seeded.Children[0];

        var deleted = await fixture.Client.DeleteAsync(new Uri($"/v1/family/children/{target.ChildId}", UriKind.Relative));
        var edited = await fixture.Client.PutAsJsonAsync(
            new Uri($"/v1/family/children/{target.ChildId}", UriKind.Relative),
            new { Name = "Hijacked", School = "locked-school", BirthYear = target.BirthYear }
        );

        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edited.StatusCode);
        var after = await SeededMeAsync("dev-parent-1");
        Assert.Equal(seeded.Children.Select(c => (c.ChildId, c.Name)), after.Children.Select(c => (c.ChildId, c.Name)));
    }

    private static async Task<FamilyModel> SeededMeAsync(string uid)
    {
        var token = await EmulatorTokens.MintAsync(Environment.GetEnvironmentVariable("E2E_AUTH_EMULATOR")!, uid);
        using var client = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("E2E_BASE_URL")!) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        return (await client.GetFromJsonAsync<FamilyModel>(Me))!;
    }
}
