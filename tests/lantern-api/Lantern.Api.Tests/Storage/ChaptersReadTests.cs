using Azure.Storage.Blobs;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Repository;

namespace Lantern.Api.Tests.Storage;

[Collection(ApiCollection.Name)]
public sealed class ChaptersReadTests(AzuriteFixture azurite)
{
    private static readonly Chapter[] English = [new("gepr101", "gepr1", 1, "A Bottle of Dew", "A story."), new("gepr102", "gepr1", 2, "A Tale of Two Birds", "Two birds.")];

    private static readonly Subject EnglishSubject = new("english", "English", "english", 2);

    private readonly BlobContainerClient family = azurite.CreateBlobClient().GetBlobContainerClient($"family{Guid.NewGuid():N}");

    [Fact]
    public async Task ChaptersAsync_ASubjectInTheSlice_ReturnsItsChaptersInTheOrderTheManifestHolds()
    {
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        await FamilyManifestSeed.PutSliceAsync(family, familyId, childId, 7, (EnglishSubject, English));

        Assert.Equal(English, await Store().ChaptersAsync(familyId, childId, 7, "english", CancellationToken.None));
    }

    [Fact]
    public async Task ChaptersAsync_ASubjectTheSliceDoesNotHave_ReturnsNull()
    {
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        await FamilyManifestSeed.PutSliceAsync(family, familyId, childId, 7, (EnglishSubject, English));

        Assert.Null(await Store().ChaptersAsync(familyId, childId, 7, "astronomy", CancellationToken.None));
    }

    [Fact]
    public async Task ChaptersAsync_ASliceWithNoManifest_ReturnsNull()
    {
        await family.CreateIfNotExistsAsync();

        Assert.Null(await Store().ChaptersAsync(Guid.NewGuid(), Guid.NewGuid(), 7, "english", CancellationToken.None));
    }

    [Fact]
    public async Task ChaptersAsync_AnotherChildsSlice_IsNeverRead()
    {
        var (familyId, theirs) = (Guid.NewGuid(), Guid.NewGuid());
        await FamilyManifestSeed.PutSliceAsync(family, familyId, theirs, 7, (EnglishSubject, English));

        Assert.Null(await Store().ChaptersAsync(familyId, Guid.NewGuid(), 7, "english", CancellationToken.None));
    }

    [Fact]
    public async Task ChaptersAsync_ASchemaItDoesNotKnow_Throws_NeverAnEmptyAnswer()
    {
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        await FamilyManifestSeed.PutSliceRawAsync(family, familyId, childId, 7, FamilyManifestSeed.ClassManifestJson("class-manifest-v9", 7, (EnglishSubject, English)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().ChaptersAsync(familyId, childId, 7, "english", CancellationToken.None));
    }

    private WorkspaceStore Store() => new(family, new Dictionary<BoardType, BlobContainerClient>(), TimeProvider.System, TestServices.Json, TestServices.Mapper, TestServices.Layout, createContainer: true);
}
