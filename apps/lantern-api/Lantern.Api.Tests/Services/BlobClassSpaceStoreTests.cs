using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lantern.Api.Services;
using Lantern.Api.Tests.Infrastructure;

namespace Lantern.Api.Tests.Services;

[Collection(ApiCollection.Name)]
// Real Azurite: If-None-Match behaviour is emulator behaviour a fake would hide.
public sealed class BlobClassSpaceStoreTests(AzuriteFixture azurite)
{
    private readonly BlobContainerClient container = azurite.CreateBlobClient().GetBlobContainerClient($"family{Guid.NewGuid():N}");

    private BlobClassSpaceStore Store => new(container, createContainer: true);

    [Fact]
    public async Task StartAsync_CreatesMarkerUnderTheChildsClassPath()
    {
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        await Store.StartAsync(familyId, childId, 6, CancellationToken.None);

        var marker = container.GetBlobClient($"{familyId:D}/{childId:D}/6/class.json");
        Assert.True(await marker.ExistsAsync());
    }

    [Fact]
    public async Task StartAsync_DifferentClasses_GetSeparateSpaces()
    {
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        await Store.StartAsync(familyId, childId, 6, CancellationToken.None);
        await Store.StartAsync(familyId, childId, 7, CancellationToken.None);

        Assert.True(await container.GetBlobClient($"{familyId:D}/{childId:D}/6/class.json").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{familyId:D}/{childId:D}/7/class.json").ExistsAsync());
    }

    [Fact]
    public async Task StartAsync_RepeatedClass_ReusesTheSpaceWithoutRewritingTheMarker()
    {
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var marker = container.GetBlobClient($"{familyId:D}/{childId:D}/6/class.json");

        await Store.StartAsync(familyId, childId, 6, CancellationToken.None);
        var first = (await marker.GetPropertiesAsync()).Value.ETag;

        await Store.StartAsync(familyId, childId, 6, CancellationToken.None);

        Assert.Equal(first, (await marker.GetPropertiesAsync()).Value.ETag);
    }

    [Fact]
    public async Task DeleteChildAsync_RemovesEverySpaceOfThatChildAndNothingElse()
    {
        var familyId = Guid.NewGuid();
        var child = Guid.NewGuid();
        var sibling = Guid.NewGuid();
        var otherFamily = Guid.NewGuid();
        await Store.StartAsync(familyId, child, 5, CancellationToken.None);
        await Store.StartAsync(familyId, child, 6, CancellationToken.None);
        await Store.StartAsync(familyId, sibling, 5, CancellationToken.None);
        await Store.StartAsync(otherFamily, child, 5, CancellationToken.None);
        await container.GetBlobClient($"{familyId:D}/{child:D}/6/notes/page.txt").UploadAsync(BinaryData.FromString("x"));

        await Store.DeleteChildAsync(familyId, child, CancellationToken.None);

        Assert.Empty(await BlobsUnder($"{familyId:D}/{child:D}/"));
        Assert.True(await container.GetBlobClient($"{familyId:D}/{sibling:D}/5/class.json").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{otherFamily:D}/{child:D}/5/class.json").ExistsAsync());
    }

    [Fact]
    public async Task DeleteChildAsync_Repeated_Succeeds()
    {
        var familyId = Guid.NewGuid();
        var child = Guid.NewGuid();
        await Store.StartAsync(familyId, child, 5, CancellationToken.None);

        await Store.DeleteChildAsync(familyId, child, CancellationToken.None);
        await Store.DeleteChildAsync(familyId, child, CancellationToken.None);

        Assert.Empty(await BlobsUnder($"{familyId:D}/"));
    }

    [Fact]
    public async Task DeleteChildAsync_WhenNoSpaceWasEverStarted_Succeeds()
    {
        var neverCreated = azurite.CreateBlobClient().GetBlobContainerClient($"family{Guid.NewGuid():N}");

        await new BlobClassSpaceStore(neverCreated, createContainer: false).DeleteChildAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task DeleteChildAsync_WithManyBlobs_RemovesAll()
    {
        var familyId = Guid.NewGuid();
        var child = Guid.NewGuid();
        await Store.StartAsync(familyId, child, 5, CancellationToken.None);
        foreach (var n in Enumerable.Range(0, 40))
        {
            await container.GetBlobClient($"{familyId:D}/{child:D}/5/f{n}.txt").UploadAsync(BinaryData.FromString("x"));
        }

        await Store.DeleteChildAsync(familyId, child, CancellationToken.None);

        Assert.Empty(await BlobsUnder($"{familyId:D}/{child:D}/"));
    }

    private Task<List<string>> BlobsUnder(string prefix) =>
        container
            .GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None)
            .Select(blob => blob.Name)
            .ToListAsync()
            .AsTask();
}
