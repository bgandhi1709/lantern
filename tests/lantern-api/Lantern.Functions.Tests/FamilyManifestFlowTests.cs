using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Functions.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Functions.Tests;

[Collection(FunctionsCollection.Name)]
public sealed class FamilyManifestFlowTests(AzuriteFixture azurite) : IDisposable
{
    private readonly FunctionsHost host = new(azurite.ConnectionString);

    private BlobContainerClient Family => azurite.CreateBlobClient().GetBlobContainerClient(TestSettings.WorkspaceContainer);

    [Fact]
    public async Task Register_ThreeChildren_EndsWithAllThreeEntriesInOneManifest()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3, 6, 6);

        await host.DeliverAsync();

        AssertJson(Manifest("cbse", family.Children.Select(child => Ncert(child.ChildId, child.ClassLevel))), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task Register_AnSscFamily_CarriesTheSscSubjectsAndProfile()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Ssc, 2);

        await host.DeliverAsync();

        AssertJson(Manifest("ssc", [Ssc(family.Children.Single().ChildId, 2)]), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task AddChild_AddsTheEntryLater_AndKeepsTheFirst()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3);
        await host.DeliverAsync();
        var added = Guid.NewGuid();

        await client.Children.AddAsync(NewChild(added, 4), CancellationToken.None);
        await host.DeliverAsync();

        AssertJson(Manifest("cbse", [Ncert(family.Children[0].ChildId, 3), Ncert(added, 4)]), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task EveryMessageDeliveredAgain_ChangesNothing()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3, 4);
        await host.DeliverAsync();
        var before = await ManifestEtagAsync(family.FamilyId);

        foreach (var message in host.Sender.Sent.Where(sent => sent.Type == ActionType.CreateWorkspace))
        {
            await host.Services.DeliverAsync(message, CancellationToken.None);
        }

        Assert.Equal(before, await ManifestEtagAsync(family.FamilyId));
        AssertJson(Manifest("cbse", family.Children.Select(child => Ncert(child.ChildId, child.ClassLevel))), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task DeletingAChild_RemovesOnlyItsEntry()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3, 6);
        await host.DeliverAsync();
        var (gone, kept) = (family.Children[0], family.Children[1]);

        await client.Children.RemoveAsync(gone.ChildId, CancellationToken.None);
        await host.DeliverAsync();

        AssertJson(Manifest("cbse", [Ncert(kept.ChildId, 6)]), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task DeletingTheFamily_LeavesNoManifest()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3, 6);
        await host.DeliverAsync();

        await client.EraseFamilyAsync();
        await host.DeliverAsync();

        Assert.False(await Family.GetBlobClient($"{family.FamilyId:D}/manifest.json").ExistsAsync());
    }

    [Fact]
    public async Task EditingAChild_LeavesTheManifestUntouched()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3);
        await host.DeliverAsync();
        var before = await ManifestEtagAsync(family.FamilyId);
        var child = family.Children.Single();

        child.Name = Locked("Renamed");
        child.BirthYear = Locked("2017");
        child.School = Locked("Other School");
        await client.Children.UpdateAsync(child, CancellationToken.None);

        Assert.Equal(before, await ManifestEtagAsync(family.FamilyId));
    }

    [Fact]
    public async Task AChildDeletedBeforeItsMessageRuns_NeverGetsAnEntry()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3);
        await host.DeliverAsync();
        var added = Guid.NewGuid();
        await client.Children.AddAsync(NewChild(added, 4), CancellationToken.None);

        await client.Children.RemoveAsync(added, CancellationToken.None);
        await host.DeliverAsync();

        AssertJson(Manifest("cbse", [Ncert(family.Children[0].ChildId, 3)]), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task WhenTheManifestWriteFailsAfterTheSliceIsReady_TheRedeliveryAddsTheEntry()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3);
        var outage = new ManifestOutageStore(host.Services.GetRequiredService<IWorkspaceStore>(), failAdd: true);
        using var broken = new FunctionsHost(azurite.ConnectionString, configure: services => services.AddSingleton<IWorkspaceStore>(outage));
        var message = host.Sender.Sent.Single(sent => sent.Type == ActionType.CreateWorkspace);

        await Assert.ThrowsAsync<IOException>(() => broken.Services.DeliverAsync(message, CancellationToken.None));

        Assert.True(await Family.GetBlobClient($"{family.FamilyId:D}/{family.Children[0].ChildId:D}/3/content/version.json").ExistsAsync());
        Assert.False(await Family.GetBlobClient($"{family.FamilyId:D}/manifest.json").ExistsAsync());

        await host.DeliverAsync();

        AssertJson(Manifest("cbse", [Ncert(family.Children[0].ChildId, 3)]), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task AChildDeletedWhileTheirSliceWasBeingCopied_IsNeverAddedToTheManifest_SoNothingIsWrittenOnlyToBeUndone()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3);
        var child = family.Children.Single();
        var deleteIt = new ManifestOutageStore(
            host.Services.GetRequiredService<IWorkspaceStore>(),
            afterCopy: () => client.Children.RemoveAsync(child.ChildId, CancellationToken.None)
        );
        using var racing = new FunctionsHost(azurite.ConnectionString, configure: services => services.AddSingleton<IWorkspaceStore>(deleteIt));

        await racing.Services.DeliverAsync(host.Sender.Sent.Single(sent => sent.Type == ActionType.CreateWorkspace), CancellationToken.None);

        Assert.Equal(0, deleteIt.AddCalls);
        Assert.False(await Family.GetBlobClient($"{family.FamilyId:D}/manifest.json").ExistsAsync());
        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{child.ChildId:D}/"));
    }

    [Fact]
    public async Task AChildDeletedJustAfterTheirEntryWasAdded_IsCleanedUpByTheRemoveMessage()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3);
        var child = family.Children.Single();
        var deleteIt = new ManifestOutageStore(
            host.Services.GetRequiredService<IWorkspaceStore>(),
            afterAdd: () => client.Children.RemoveAsync(child.ChildId, CancellationToken.None)
        );
        using var racing = new FunctionsHost(azurite.ConnectionString, configure: services => services.AddSingleton<IWorkspaceStore>(deleteIt));

        await racing.Services.DeliverAsync(host.Sender.Sent.Single(sent => sent.Type == ActionType.CreateWorkspace), CancellationToken.None);
        await host.DeliverAsync();

        Assert.Null(TestServices.FamilyManifest(await ManifestAsync(family.FamilyId)).SubjectsOf(child.ChildId));
        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{child.ChildId:D}/"));
    }

    [Fact]
    public async Task AChildFullyRemovedWhileTheirSliceWasBeingCopied_LeavesNothingBehind_BecauseTheCreateChecksAfterItsCopy()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3);
        var child = family.Children.Single();
        var inner = host.Services.GetRequiredService<IWorkspaceStore>();
        var removeFirst = new ManifestOutageStore(
            inner,
            beforeCopy: async () =>
            {
                await client.Children.RemoveAsync(child.ChildId, CancellationToken.None);
                await host.Services.DeliverAsync(host.Sender.Sent.Single(sent => sent.Type == ActionType.RemoveWorkspace), CancellationToken.None);
            }
        );
        using var racing = new FunctionsHost(azurite.ConnectionString, configure: services => services.AddSingleton<IWorkspaceStore>(removeFirst));

        await racing.Services.DeliverAsync(host.Sender.Sent.Single(sent => sent.Type == ActionType.CreateWorkspace), CancellationToken.None);

        Assert.Equal(0, removeFirst.AddCalls);
        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{child.ChildId:D}/"));
        Assert.False(await Family.GetBlobClient($"{family.FamilyId:D}/manifest.json").ExistsAsync());
    }

    [Theory]
    [InlineData("beforeCreate")]
    [InlineData("afterCopy")]
    public async Task AChildFullyRemovedAtAnyPointOfTheirCreate_LeavesNothingBehind_WithoutASecondSweep(string point)
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3);
        var child = family.Children.Single();
        async Task DeleteAndRemoveAsync()
        {
            await client.Children.RemoveAsync(child.ChildId, CancellationToken.None);
            await host.Services.DeliverAsync(host.Sender.Sent.Single(sent => sent.Type == ActionType.RemoveWorkspace), CancellationToken.None);
        }

        var inner = host.Services.GetRequiredService<IWorkspaceStore>();
        var store = point == "beforeCreate" ? new ManifestOutageStore(inner, beforeCreate: DeleteAndRemoveAsync) : new ManifestOutageStore(inner, afterCopy: DeleteAndRemoveAsync);
        using var racing = new FunctionsHost(azurite.ConnectionString, configure: services => services.AddSingleton<IWorkspaceStore>(store));

        await racing.Services.DeliverAsync(host.Sender.Sent.Single(sent => sent.Type == ActionType.CreateWorkspace), CancellationToken.None);

        Assert.Equal(0, store.AddCalls);
        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{child.ChildId:D}/"));
        Assert.False(await Family.GetBlobClient($"{family.FamilyId:D}/manifest.json").ExistsAsync());
    }

    [Fact]
    public async Task DeletingAChild_IsOneCallToTheStore_AndTheEntryIsGoneWithTheWorkspace()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 3, 6);
        await host.DeliverAsync();
        var (gone, kept) = (family.Children[0], family.Children[1]);
        var counting = new ManifestOutageStore(host.Services.GetRequiredService<IWorkspaceStore>());
        using var counted = new FunctionsHost(azurite.ConnectionString, configure: services => services.AddSingleton<IWorkspaceStore>(counting));
        await client.Children.RemoveAsync(gone.ChildId, CancellationToken.None);

        await counted.Services.DeliverAsync(host.Sender.Sent.Single(sent => sent.Type == ActionType.RemoveWorkspace), CancellationToken.None);

        Assert.Equal(1, counting.RemoveCalls);
        AssertJson(Manifest("cbse", [Ncert(kept.ChildId, 6)]), await ManifestAsync(family.FamilyId));
        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{gone.ChildId:D}/"));
    }

    public void Dispose() => host.Dispose();

    private static (Guid ChildId, int ClassLevel, string Profile, string Subjects) Ncert(Guid childId, int classLevel) =>
        (childId, classLevel, "cbse-v1", """[ { "subjectId": "mathematics", "name": "Mathematics", "icon": "maths", "chapterCount": 2 } ]""");

    private static (Guid ChildId, int ClassLevel, string Profile, string Subjects) Ssc(Guid childId, int classLevel) =>
        (
            childId,
            classLevel,
            "ssc-v2",
            """
            [
              { "subjectId": "mathematics", "name": "Mathematics", "icon": "maths",   "chapterCount": 1 },
              { "subjectId": "english",     "name": "English",     "icon": "english", "chapterCount": 1 }
            ]
            """
        );

    private static string Manifest(string board, IEnumerable<(Guid ChildId, int ClassLevel, string Profile, string Subjects)> children) =>
        $$"""
        { "schema": "family-manifest-v1", "board": "{{board}}", "children": { {{string.Join(",", children.Select(child => $$"""
          "{{child.ChildId:D}}": { "class": {{child.ClassLevel}}, "profileVersion": "{{child.Profile}}", "subjects": {{child.Subjects}} }
          """))}} } }
        """;

    private static void AssertJson(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"Expected {expected}{Environment.NewLine}Actual {actual}");

    private async Task<string> ManifestAsync(Guid familyId) =>
        (await Family.GetBlobClient($"{familyId:D}/manifest.json").DownloadContentAsync()).Value.Content.ToString();

    private async Task<string> ManifestEtagAsync(Guid familyId) =>
        (await Family.GetBlobClient($"{familyId:D}/manifest.json").GetPropertiesAsync()).Value.ETag.ToString();

    private async Task<string[]> NamesAsync(string prefix)
    {
        var names = new List<string>();
        await foreach (var blob in Family.GetBlobsAsync(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, prefix, CancellationToken.None))
        {
            names.Add(blob.Name);
        }

        return [.. names];
    }

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static string Locked(string plaintext) => $"v1.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext))}.{Guid.NewGuid():N}";

    private static Child NewChild(Guid id, int classLevel) =>
        new() { ChildId = id, Name = Locked("Kavya"), ClassLevel = classLevel, BirthYear = Locked("2018"), School = Locked("Green School") };

    private sealed class ManifestOutageStore(
        IWorkspaceStore inner,
        bool failAdd = false,
        Func<Task> beforeCreate = null,
        Func<Task> beforeCopy = null,
        Func<Task> afterCopy = null,
        Func<Task> afterAdd = null
    )
        : IWorkspaceStore
    {
        public int AddCalls { get; private set; }

        public int RemoveCalls { get; private set; }

        public async Task CreateAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken)
        {
            if (beforeCreate is not null)
            {
                await beforeCreate();
            }

            await inner.CreateAsync(familyId, childId, classLevel, cancellationToken);
        }

        public async Task CopyContentAsync(Guid familyId, Guid childId, int classLevel, BoardType board, CancellationToken cancellationToken)
        {
            if (beforeCopy is not null)
            {
                await beforeCopy();
            }

            await inner.CopyContentAsync(familyId, childId, classLevel, board, cancellationToken);
            if (afterCopy is not null)
            {
                await afterCopy();
            }
        }

        public Task<IReadOnlyList<Subject>> SubjectsAsync(Guid familyId, Guid childId, CancellationToken cancellationToken) =>
            inner.SubjectsAsync(familyId, childId, cancellationToken);

        public Task<ClassManifest> ClassManifestAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken) =>
            inner.ClassManifestAsync(familyId, childId, classLevel, cancellationToken);

        public Task<IReadOnlyList<Chapter>> ChaptersAsync(Guid familyId, Guid childId, int classLevel, string subjectId, CancellationToken cancellationToken) =>
            inner.ChaptersAsync(familyId, childId, classLevel, subjectId, cancellationToken);

        public async Task AddToManifestAsync(Guid familyId, Guid childId, int classLevel, BoardType board, CancellationToken cancellationToken)
        {
            AddCalls++;
            if (failAdd)
            {
                throw new IOException("storage down");
            }

            await inner.AddToManifestAsync(familyId, childId, classLevel, board, cancellationToken);
            if (afterAdd is not null)
            {
                await afterAdd();
            }
        }

        public Task RemoveAsync(Guid familyId, Guid childId, CancellationToken cancellationToken)
        {
            RemoveCalls++;

            return inner.RemoveAsync(familyId, childId, cancellationToken);
        }

        public Task RemoveFamilyAsync(Guid familyId, CancellationToken cancellationToken) => inner.RemoveFamilyAsync(familyId, cancellationToken);
    }
}
