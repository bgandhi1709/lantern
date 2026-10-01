using Azure.Storage.Blobs;
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
}
