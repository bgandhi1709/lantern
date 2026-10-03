using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Lantern.Api.Test.Integration.E2E;

// Needs the local Docker stack (deploy/local). Its own class, so its own registered Family: the others keep theirs.
[Trait("Category", "E2E")]
public sealed class FamilyDeleteE2ETests(RegisteredFamilyFixture fixture) : IClassFixture<RegisteredFamilyFixture>
{
    private static readonly Uri Family = new("/v1/family", UriKind.Relative);
    private static readonly Uri Me = new("/v1/me", UriKind.Relative);

    // Azurite's published development account; not a secret.
    private const string AzuriteBlobs =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
        + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
        + "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    [Fact]
    public async Task DeleteTheFamily_LocksTheCallerOut_AndTheWorkerErasesItsWorkspaces()
    {
        var container = new BlobServiceClient(AzuriteBlobs).GetBlobContainerClient("family");
        var prefix = $"{fixture.Family.FamilyId:D}/";
        Assert.True(await WaitAsync(() => AnyAsync(container, prefix)), "The worker did not start the Workspace in time.");

        var deleted = await fixture.Client.DeleteAsync(Family);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync(Me)).StatusCode);
        Assert.True(await WaitAsync(async () => !await AnyAsync(container, prefix)), "The worker did not erase the Family in time.");
    }

    private static async Task<bool> AnyAsync(BlobContainerClient container, string prefix) =>
        await container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None).AnyAsync();

    private static async Task<bool> WaitAsync(Func<Task<bool>> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline && !await done())
        {
            await Task.Delay(500);
        }

        return await done();
    }
}
