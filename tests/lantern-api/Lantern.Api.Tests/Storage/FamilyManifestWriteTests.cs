using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Lantern.Core.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Repository;

namespace Lantern.Api.Tests.Storage;

[Collection(ApiCollection.Name)]
public sealed class FamilyManifestWriteTests(AzuriteFixture azurite)
{
    private const string ClassThreeSubjects = """
        [
          { "subjectId": "mathematics", "name": "Mathematics", "icon": "maths",   "chapterCount": 1 },
          { "subjectId": "english",     "name": "English",     "icon": "english", "chapterCount": 2 }
        ]
        """;

    private const string ClassFourSubjects = """
        [
          { "subjectId": "english", "name": "English", "icon": "english", "chapterCount": 2 }
        ]
        """;

    private readonly BlobContainerClient family = azurite.CreateBlobClient().GetBlobContainerClient($"family{Guid.NewGuid():N}");

    [Fact]
    public async Task AddToManifestAsync_TheFirstChild_CreatesTheManifestWithTheirSubjects()
    {
        var store = Store(await BoardMaster.NcertAsync(azurite));
        var (familyId, child) = (Guid.NewGuid(), Guid.NewGuid());
        await ReadyAsync(store, familyId, child, 3);

        await store.AddToManifestAsync(familyId, child, 3, BoardType.Cbse, CancellationToken.None);

        AssertJson(Manifest((child, 3, ClassThreeSubjects)), await ManifestAsync(familyId));
    }

    [Fact]
    public async Task AddToManifestAsync_ASecondChild_AddsAnEntryAndKeepsTheFirst()
    {
        var store = Store(await BoardMaster.NcertAsync(azurite));
        var (familyId, first, second) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await ReadyAsync(store, familyId, first, 3);
        await ReadyAsync(store, familyId, second, 4);

        await store.AddToManifestAsync(familyId, first, 3, BoardType.Cbse, CancellationToken.None);
        await store.AddToManifestAsync(familyId, second, 4, BoardType.Cbse, CancellationToken.None);

        AssertJson(Manifest((first, 3, ClassThreeSubjects), (second, 4, ClassFourSubjects)), await ManifestAsync(familyId));
    }

    [Fact]
    public async Task AddToManifestAsync_ASliceThatIsNotReady_Throws_AndWritesNothing()
    {
        var store = Store(await BoardMaster.NcertAsync(azurite));
        var (familyId, child) = (Guid.NewGuid(), Guid.NewGuid());
        await store.CreateAsync(familyId, child, 3, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddToManifestAsync(familyId, child, 3, BoardType.Cbse, CancellationToken.None));

        Assert.False(await family.GetBlobClient($"{familyId:D}/manifest.json").ExistsAsync());
    }

    [Fact]
    public async Task AddToManifestAsync_TheSameChildAgain_DoesNotRewriteTheFile()
    {
        var store = Store(await BoardMaster.NcertAsync(azurite));
        var (familyId, child) = (Guid.NewGuid(), Guid.NewGuid());
        await ReadyAsync(store, familyId, child, 3);
        await store.AddToManifestAsync(familyId, child, 3, BoardType.Cbse, CancellationToken.None);
        var before = (await family.GetBlobClient($"{familyId:D}/manifest.json").GetPropertiesAsync()).Value.ETag;

        await store.AddToManifestAsync(familyId, child, 3, BoardType.Cbse, CancellationToken.None);

        Assert.Equal(before, (await family.GetBlobClient($"{familyId:D}/manifest.json").GetPropertiesAsync()).Value.ETag);
    }

    [Fact]
    public async Task AddToManifestAsync_SixChildrenFinishingAtOnce_AllEndUpInTheManifest()
    {
        var store = Store(await BoardMaster.NcertAsync(azurite));
        var familyId = Guid.NewGuid();
        (Guid Id, int ClassLevel)[] children = [.. Enumerable.Range(0, 6).Select(n => (Guid.NewGuid(), n % 2 == 0 ? 3 : 4))];
        foreach (var (id, classLevel) in children)
        {
            await ReadyAsync(store, familyId, id, classLevel);
        }

        await Task.WhenAll(children.Select(child => store.AddToManifestAsync(familyId, child.Id, child.ClassLevel, BoardType.Cbse, CancellationToken.None)));

        AssertJson(
            Manifest([.. children.Select(child => (child.Id, child.ClassLevel, child.ClassLevel == 3 ? ClassThreeSubjects : ClassFourSubjects))]),
            await ManifestAsync(familyId)
        );
    }

    [Fact]
    public async Task AddToManifestAsync_WhenASiblingCreatesTheFileFirst_ReadsItAgainAndKeepsBothEntries()
    {
        var master = await BoardMaster.NcertAsync(azurite);
        var (familyId, mine, sibling) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var rival = new RivalWrite($"{familyId:D}/manifest.json", () => FamilyManifestSeed.PutAsync(family, familyId, (sibling, 4, ClassFour)), times: 1);
        var store = Store(master, Racing(rival));
        await ReadyAsync(store, familyId, mine, 3);

        await store.AddToManifestAsync(familyId, mine, 3, BoardType.Cbse, CancellationToken.None);

        Assert.Equal(1, rival.Fired);
        AssertJson(Manifest((sibling, 4, ClassFourSubjects), (mine, 3, ClassThreeSubjects)), await ManifestAsync(familyId));
    }

    [Fact]
    public async Task AddToManifestAsync_WhenASiblingUpdatesTheFileBetweenTheReadAndTheWrite_ReadsItAgainAndKeepsBothEntries()
    {
        var master = await BoardMaster.NcertAsync(azurite);
        var (familyId, first, sibling, mine) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await FamilyManifestSeed.PutAsync(family, familyId, (first, 3, ClassThree));
        var rival = new RivalWrite($"{familyId:D}/manifest.json", () => FamilyManifestSeed.PutAsync(family, familyId, (first, 3, ClassThree), (sibling, 4, ClassFour)), times: 1);
        var store = Store(master, Racing(rival));
        await ReadyAsync(store, familyId, mine, 3);

        await store.AddToManifestAsync(familyId, mine, 3, BoardType.Cbse, CancellationToken.None);

        Assert.Equal(1, rival.Fired);
        AssertJson(Manifest((first, 3, ClassThreeSubjects), (sibling, 4, ClassFourSubjects), (mine, 3, ClassThreeSubjects)), await ManifestAsync(familyId));
    }

    [Fact]
    public async Task AddToManifestAsync_WhenTheFileKeepsChangingUnderIt_GivesUpWithAnErrorSoTheQueueRetriesLater()
    {
        var master = await BoardMaster.NcertAsync(azurite);
        var (familyId, mine) = (Guid.NewGuid(), Guid.NewGuid());
        var rival = new RivalWrite($"{familyId:D}/manifest.json", () => FamilyManifestSeed.PutAsync(family, familyId, (Guid.NewGuid(), 4, ClassFour)), times: int.MaxValue);
        var store = Store(master, Racing(rival));
        await ReadyAsync(store, familyId, mine, 3);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddToManifestAsync(familyId, mine, 3, BoardType.Cbse, CancellationToken.None));

        Assert.InRange(rival.Fired, 2, 50);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTheChildsWorkspaceAndOnlyTheirManifestEntry_InOneCall()
    {
        var store = Store(await BoardMaster.NcertAsync(azurite));
        var (familyId, stays, goes) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await ReadyAsync(store, familyId, stays, 3);
        await ReadyAsync(store, familyId, goes, 4);
        await store.AddToManifestAsync(familyId, stays, 3, BoardType.Cbse, CancellationToken.None);
        await store.AddToManifestAsync(familyId, goes, 4, BoardType.Cbse, CancellationToken.None);

        await store.RemoveAsync(familyId, goes, CancellationToken.None);

        AssertJson(Manifest((stays, 3, ClassThreeSubjects)), await ManifestAsync(familyId));
        Assert.False(await family.GetBlobClient($"{familyId:D}/{goes:D}/4/content/version.json").ExistsAsync());
        Assert.True(await family.GetBlobClient($"{familyId:D}/{stays:D}/3/content/version.json").ExistsAsync());
    }

    [Fact]
    public async Task RemoveAsync_AChildWithNoEntry_RewritesNothing_AndAFamilyWithNoManifestGetsNone()
    {
        var store = Store(await BoardMaster.NcertAsync(azurite));
        var (familyId, child) = (Guid.NewGuid(), Guid.NewGuid());
        await store.RemoveAsync(familyId, child, CancellationToken.None);
        Assert.False(await family.GetBlobClient($"{familyId:D}/manifest.json").ExistsAsync());
        await ReadyAsync(store, familyId, child, 3);
        await store.AddToManifestAsync(familyId, child, 3, BoardType.Cbse, CancellationToken.None);
        var before = (await family.GetBlobClient($"{familyId:D}/manifest.json").GetPropertiesAsync()).Value.ETag;

        await store.RemoveAsync(familyId, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(before, (await family.GetBlobClient($"{familyId:D}/manifest.json").GetPropertiesAsync()).Value.ETag);
    }

    [Fact]
    public async Task RemoveAsync_RepeatedAfterTheChildIsGone_ChangesNothing()
    {
        var store = Store(await BoardMaster.NcertAsync(azurite));
        var (familyId, stays, goes) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await ReadyAsync(store, familyId, stays, 3);
        await ReadyAsync(store, familyId, goes, 4);
        await store.AddToManifestAsync(familyId, stays, 3, BoardType.Cbse, CancellationToken.None);
        await store.AddToManifestAsync(familyId, goes, 4, BoardType.Cbse, CancellationToken.None);
        await store.RemoveAsync(familyId, goes, CancellationToken.None);
        var before = (await family.GetBlobClient($"{familyId:D}/manifest.json").GetPropertiesAsync()).Value.ETag;

        await store.RemoveAsync(familyId, goes, CancellationToken.None);

        Assert.Equal(before, (await family.GetBlobClient($"{familyId:D}/manifest.json").GetPropertiesAsync()).Value.ETag);
        AssertJson(Manifest((stays, 3, ClassThreeSubjects)), await ManifestAsync(familyId));
    }

    [Fact]
    public async Task RemoveFamilyAsync_AlsoErasesTheManifest_ButRemovingOneChildLeavesTheFileForTheOthers()
    {
        var store = Store(await BoardMaster.NcertAsync(azurite));
        var (familyId, child) = (Guid.NewGuid(), Guid.NewGuid());
        await ReadyAsync(store, familyId, child, 3);
        await store.AddToManifestAsync(familyId, child, 3, BoardType.Cbse, CancellationToken.None);

        await store.RemoveAsync(familyId, child, CancellationToken.None);
        Assert.True(await family.GetBlobClient($"{familyId:D}/manifest.json").ExistsAsync());

        await store.RemoveFamilyAsync(familyId, CancellationToken.None);
        Assert.False(await family.GetBlobClient($"{familyId:D}/manifest.json").ExistsAsync());
    }

    private static readonly Subject[] ClassFour = [new("english", "English", "english", 2)];

    private static readonly Subject[] ClassThree = [new("mathematics", "Mathematics", "maths", 1), new("english", "English", "english", 2)];

    private BlobContainerClient Racing(RivalWrite rival) =>
        new BlobServiceClient(azurite.ConnectionString, OptionsWith(rival)).GetBlobContainerClient(family.Name);

    private static BlobClientOptions OptionsWith(HttpPipelinePolicy policy)
    {
        var options = new BlobClientOptions();
        options.Retry.MaxRetries = 0;
        options.AddPolicy(policy, HttpPipelinePosition.PerCall);

        return options;
    }

    private sealed class RivalWrite(string blobName, Func<Task> write, int times) : HttpPipelinePolicy
    {
        private int fired;

        public int Fired => fired;

        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline) => throw new NotSupportedException("Async only.");

        public override async ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            if (message.Request.Method == RequestMethod.Put && message.Request.Uri.Path.EndsWith("/" + blobName, StringComparison.Ordinal) && fired < times)
            {
                Interlocked.Increment(ref fired);
                await write();
            }

            await ProcessNextAsync(message, pipeline);
        }
    }

    private WorkspaceStore Store(BoardMaster ncert, BlobContainerClient familyOverride = null) =>
        new(familyOverride ?? family, new Dictionary<BoardType, BlobContainerClient> { [BoardType.Cbse] = ncert.Container }, TimeProvider.System, TestServices.Json, TestServices.Mapper, TestServices.Layout, createContainer: true);

    private static async Task ReadyAsync(WorkspaceStore store, Guid familyId, Guid childId, int classLevel)
    {
        await store.CreateAsync(familyId, childId, classLevel, CancellationToken.None);
        await store.CopyContentAsync(familyId, childId, classLevel, BoardType.Cbse, CancellationToken.None);
    }

    private async Task<string> ManifestAsync(Guid familyId) =>
        (await family.GetBlobClient($"{familyId:D}/manifest.json").DownloadContentAsync()).Value.Content.ToString();

    private static string Manifest(params (Guid ChildId, int ClassLevel, string Subjects)[] children) =>
        $$"""
        {
          "schema": "family-manifest-v1",
          "board": "cbse",
          "children": { {{string.Join(",", children.Select(child => $$"""
            "{{child.ChildId:D}}": { "class": {{child.ClassLevel}}, "profileVersion": "cbse-v1", "subjects": {{child.Subjects}} }
            """))}} }
        }
        """;

    private static void AssertJson(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"Expected {expected}{Environment.NewLine}Actual {actual}");
}
