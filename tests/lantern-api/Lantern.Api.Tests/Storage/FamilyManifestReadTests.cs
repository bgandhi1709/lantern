using Azure.Storage.Blobs;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Repository;

namespace Lantern.Api.Tests.Storage;

[Collection(ApiCollection.Name)]
public sealed class FamilyManifestReadTests(AzuriteFixture azurite)
{
    private static readonly Subject[] ClassThree =
    [
        new("english", "English", "english", 12),
        new("mathematics", "Mathematics", "maths", 14),
        new("the-world-around-us", "The World Around Us", "evs", 12),
    ];

    private readonly BlobContainerClient family = azurite.CreateBlobClient().GetBlobContainerClient($"family{Guid.NewGuid():N}");

    [Fact]
    public async Task SubjectsAsync_AChildWithAnEntry_ReturnsItsSubjects()
    {
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        await FamilyManifestSeed.PutAsync(family, familyId, (childId, 3, ClassThree));

        Assert.Equal(ClassThree, await Store().SubjectsAsync(familyId, childId, CancellationToken.None));
    }

    [Fact]
    public async Task SubjectsAsync_AFamilyWithNoManifestYet_ReturnsNull()
    {
        await family.CreateIfNotExistsAsync();

        Assert.Null(await Store().SubjectsAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task SubjectsAsync_BeforeTheContainerExists_ReturnsNull() =>
        Assert.Null(await Store().SubjectsAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task SubjectsAsync_AChildWithNoEntry_ReturnsNull_EvenWhenSiblingsHaveOne()
    {
        var (familyId, sibling) = (Guid.NewGuid(), Guid.NewGuid());
        await FamilyManifestSeed.PutAsync(family, familyId, (sibling, 3, ClassThree));

        Assert.Null(await Store().SubjectsAsync(familyId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task SubjectsAsync_ReadsOnlyTheCallersOwnFamilysManifest()
    {
        var (mine, theirs, childId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await FamilyManifestSeed.PutAsync(family, theirs, (childId, 3, ClassThree));

        Assert.Null(await Store().SubjectsAsync(mine, childId, CancellationToken.None));
    }

    [Fact]
    public async Task SubjectsAsync_ASchemaItDoesNotKnow_Throws_NeverAnEmptyAnswer()
    {
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        await FamilyManifestSeed.PutRawAsync(family, familyId, FamilyManifestSeed.Json("family-manifest-v9", (childId, 3, ClassThree)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().SubjectsAsync(familyId, childId, CancellationToken.None));
    }

    private WorkspaceStore Store() => new(family, new Dictionary<BoardType, BlobContainerClient>(), TimeProvider.System, TestServices.Json, TestServices.Mapper, TestServices.Layout, createContainer: true);
}
