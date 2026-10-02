using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lantern.Repository;
using Lantern.Api.Tests.Infrastructure;

namespace Lantern.Api.Tests.Storage;

[Collection(ApiCollection.Name)]
// Real Azurite: If-None-Match behaviour is emulator behaviour a fake would hide.
public sealed class WorkspaceStoreTests(AzuriteFixture azurite)
{
    private readonly BlobContainerClient container = azurite.CreateBlobClient().GetBlobContainerClient($"family{Guid.NewGuid():N}");

    private WorkspaceStore Store => new(container, createContainer: true);

    [Fact]
    public async Task CreateAsync_CreatesMarkerUnderTheChildsClassPath()
    {
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        await Store.CreateAsync(familyId, childId, 6, CancellationToken.None);

        var marker = container.GetBlobClient($"{familyId:D}/{childId:D}/6/class.json");
        Assert.True(await marker.ExistsAsync());
    }

    [Fact]
    public async Task CreateAsync_DifferentClasses_GetSeparateFolders()
    {
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        await Store.CreateAsync(familyId, childId, 6, CancellationToken.None);
        await Store.CreateAsync(familyId, childId, 7, CancellationToken.None);

        Assert.True(await container.GetBlobClient($"{familyId:D}/{childId:D}/6/class.json").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{familyId:D}/{childId:D}/7/class.json").ExistsAsync());
    }

    [Fact]
    public async Task CreateAsync_RepeatedClass_ReusesTheFolderWithoutRewritingTheMarker()
    {
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var marker = container.GetBlobClient($"{familyId:D}/{childId:D}/6/class.json");

        await Store.CreateAsync(familyId, childId, 6, CancellationToken.None);
        var first = (await marker.GetPropertiesAsync()).Value.ETag;

        await Store.CreateAsync(familyId, childId, 6, CancellationToken.None);

        Assert.Equal(first, (await marker.GetPropertiesAsync()).Value.ETag);
    }

    [Fact]
    public async Task DeleteChildAsync_RemovesEverySpaceOfThatChildAndNothingElse()
    {
        var familyId = Guid.NewGuid();
        var child = Guid.NewGuid();
        var sibling = Guid.NewGuid();
        var otherFamily = Guid.NewGuid();
        await Store.CreateAsync(familyId, child, 5, CancellationToken.None);
        await Store.CreateAsync(familyId, child, 6, CancellationToken.None);
        await Store.CreateAsync(familyId, sibling, 5, CancellationToken.None);
        await Store.CreateAsync(otherFamily, child, 5, CancellationToken.None);
        await container.GetBlobClient($"{familyId:D}/{child:D}/6/notes/page.txt").UploadAsync(BinaryData.FromString("x"));

        await Store.RemoveAsync(familyId, child, CancellationToken.None);

        Assert.Empty(await BlobsUnder($"{familyId:D}/{child:D}/"));
        Assert.True(await container.GetBlobClient($"{familyId:D}/{sibling:D}/5/class.json").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{otherFamily:D}/{child:D}/5/class.json").ExistsAsync());
    }

    [Fact]
    public async Task DeleteChildAsync_Repeated_Succeeds()
    {
        var familyId = Guid.NewGuid();
        var child = Guid.NewGuid();
        await Store.CreateAsync(familyId, child, 5, CancellationToken.None);

        await Store.RemoveAsync(familyId, child, CancellationToken.None);
        await Store.RemoveAsync(familyId, child, CancellationToken.None);

        Assert.Empty(await BlobsUnder($"{familyId:D}/"));
    }

    [Fact]
    public async Task DeleteChildAsync_WhenNoSpaceWasEverStarted_Succeeds()
    {
        var neverCreated = azurite.CreateBlobClient().GetBlobContainerClient($"family{Guid.NewGuid():N}");

        await new WorkspaceStore(neverCreated, createContainer: false).RemoveAsync(
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
        await Store.CreateAsync(familyId, child, 5, CancellationToken.None);
        foreach (var n in Enumerable.Range(0, 40))
        {
            await container.GetBlobClient($"{familyId:D}/{child:D}/5/f{n}.txt").UploadAsync(BinaryData.FromString("x"));
        }

        await Store.RemoveAsync(familyId, child, CancellationToken.None);

        Assert.Empty(await BlobsUnder($"{familyId:D}/{child:D}/"));
    }

    private Task<List<string>> BlobsUnder(string prefix) =>
        container
            .GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None)
            .Select(blob => blob.Name)
            .ToListAsync()
            .AsTask();
}
