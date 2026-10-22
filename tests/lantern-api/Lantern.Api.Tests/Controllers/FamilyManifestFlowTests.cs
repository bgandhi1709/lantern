extern alias Functions;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using Functions::Lantern.Functions.Handler;
using Lantern.Api.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class FamilyManifestFlowTests(AzuriteFixture azurite) : IDisposable
{
    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    private BlobContainerClient Family => azurite.CreateBlobClient().GetBlobContainerClient(TestSettings.WorkspaceContainer);

    [Fact]
    public async Task Register_ThreeChildren_EndsWithAllThreeEntriesInOneManifest()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3, 6, 6);

        await factory.DeliverAsync();

        AssertJson(Manifest("cbse", family.Children.Select(child => Ncert(child.ChildId, child.ClassLevel))), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task Register_AnSscFamily_CarriesTheSscSubjectsAndProfile()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Ssc, 2);

        await factory.DeliverAsync();

        AssertJson(Manifest("ssc", [Ssc(family.Children.Single().ChildId, 2)]), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task AddChild_AddsTheEntryLater_AndKeepsTheFirst()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        await factory.DeliverAsync();
        var added = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(added, 4))).StatusCode);
        await factory.DeliverAsync();

        AssertJson(Manifest("cbse", [Ncert(family.Children[0].ChildId, 3), Ncert(added, 4)]), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task EveryMessageDeliveredAgain_ChangesNothing()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3, 4);
        await factory.DeliverAsync();
        var before = await ManifestEtagAsync(family.FamilyId);
        var dispatcher = factory.Services.GetRequiredService<IActionDispatcher>();

        foreach (var message in factory.Sender.Sent.Where(sent => sent.Type == ActionType.CreateWorkspace))
        {
            await dispatcher.DispatchAsync(message, CancellationToken.None);
        }

        Assert.Equal(before, await ManifestEtagAsync(family.FamilyId));
        AssertJson(Manifest("cbse", family.Children.Select(child => Ncert(child.ChildId, child.ClassLevel))), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task DeletingAChild_RemovesOnlyItsEntry()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3, 6);
        await factory.DeliverAsync();
        var (gone, kept) = (family.Children[0], family.Children[1]);

        await client.DeleteAsync(ApiClientExtensions.Child(gone.ChildId));
        await factory.DeliverAsync();

        AssertJson(Manifest("cbse", [Ncert(kept.ChildId, 6)]), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task DeletingTheFamily_LeavesNoManifest()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3, 6);
        await factory.DeliverAsync();

        await client.DeleteAsync(ApiClientExtensions.Family);
        await factory.DeliverAsync();

        Assert.False(await Family.GetBlobClient($"{family.FamilyId:D}/manifest.json").ExistsAsync());
    }

    [Fact]
    public async Task EditingAChild_LeavesTheManifestUntouched()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        await factory.DeliverAsync();
        var before = await ManifestEtagAsync(family.FamilyId);
        var child = family.Children.Single();

        var response = await client.PutAsJsonAsync(
            ApiClientExtensions.Child(child.ChildId),
            new ChildUpdateModel { Name = Locked("Renamed"), BirthYear = Locked("2017"), School = Locked("Other School") }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await ManifestEtagAsync(family.FamilyId));
    }

    [Fact]
    public async Task AChildDeletedBeforeItsMessageRuns_NeverGetsAnEntry()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        await factory.DeliverAsync();
        var added = Guid.NewGuid();
        await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(added, 4));

        await client.DeleteAsync(ApiClientExtensions.Child(added));
        await factory.DeliverAsync();

        AssertJson(Manifest("cbse", [Ncert(family.Children[0].ChildId, 3)]), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task WhenTheManifestWriteFailsAfterTheSliceIsReady_TheRedeliveryAddsTheEntry()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var outage = new ManifestOutageStore(factory.Services.GetRequiredService<IWorkspaceStore>(), failAdd: true);
        using var broken = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IWorkspaceStore>(outage)));
        var message = factory.Sender.Sent.Single(sent => sent.Type == ActionType.CreateWorkspace);

        await Assert.ThrowsAsync<IOException>(() => broken.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(message, CancellationToken.None));

        Assert.True(await Family.GetBlobClient($"{family.FamilyId:D}/{family.Children[0].ChildId:D}/3/content/version.json").ExistsAsync());
        Assert.False(await Family.GetBlobClient($"{family.FamilyId:D}/manifest.json").ExistsAsync());

        await factory.DeliverAsync();

        AssertJson(Manifest("cbse", [Ncert(family.Children[0].ChildId, 3)]), await ManifestAsync(family.FamilyId));
    }

    [Fact]
    public async Task AChildDeletedWhileTheirSliceWasBeingCopied_IsNeverAddedToTheManifest_SoNothingIsWrittenOnlyToBeUndone()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        var deleteIt = new ManifestOutageStore(
            factory.Services.GetRequiredService<IWorkspaceStore>(),
            afterCopy: () => client.DeleteAsync(ApiClientExtensions.Child(child.ChildId))
        );
        using var racing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IWorkspaceStore>(deleteIt)));

        await racing.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(factory.Sender.Sent.Single(sent => sent.Type == ActionType.CreateWorkspace), CancellationToken.None);

        Assert.Equal(0, deleteIt.AddCalls);
        Assert.False(await Family.GetBlobClient($"{family.FamilyId:D}/manifest.json").ExistsAsync());
        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{child.ChildId:D}/"));
    }

    [Fact]
    public async Task AChildDeletedJustAfterTheirEntryWasAdded_IsCleanedUpByTheRemoveMessage()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        var deleteIt = new ManifestOutageStore(
            factory.Services.GetRequiredService<IWorkspaceStore>(),
            afterAdd: () => client.DeleteAsync(ApiClientExtensions.Child(child.ChildId))
        );
        using var racing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IWorkspaceStore>(deleteIt)));

        await racing.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(factory.Sender.Sent.Single(sent => sent.Type == ActionType.CreateWorkspace), CancellationToken.None);
        await factory.DeliverAsync();

        Assert.Null(TestServices.FamilyManifest(await ManifestAsync(family.FamilyId)).SubjectsOf(child.ChildId));
        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{child.ChildId:D}/"));
    }

    [Fact]
    public async Task AChildFullyRemovedWhileTheirSliceWasBeingCopied_LeavesNothingBehind_BecauseTheCreateChecksAfterItsCopy()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        var inner = factory.Services.GetRequiredService<IWorkspaceStore>();
        var removeFirst = new ManifestOutageStore(
            inner,
            beforeCopy: async () =>
            {
                await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));
                await factory.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(factory.Sender.Sent.Single(sent => sent.Type == ActionType.RemoveWorkspace), CancellationToken.None);
            }
        );
        using var racing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IWorkspaceStore>(removeFirst)));

        await racing.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(factory.Sender.Sent.Single(sent => sent.Type == ActionType.CreateWorkspace), CancellationToken.None);

        Assert.Equal(0, removeFirst.AddCalls);
        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{child.ChildId:D}/"));
        Assert.False(await Family.GetBlobClient($"{family.FamilyId:D}/manifest.json").ExistsAsync());
    }

    [Theory]
    [InlineData("beforeCreate")]
    [InlineData("afterCopy")]
    public async Task AChildFullyRemovedAtAnyPointOfTheirCreate_LeavesNothingBehind_WithoutASecondSweep(string point)
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        async Task DeleteAndRemoveAsync()
        {
            await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));
            await factory.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(factory.Sender.Sent.Single(sent => sent.Type == ActionType.RemoveWorkspace), CancellationToken.None);
        }

        var inner = factory.Services.GetRequiredService<IWorkspaceStore>();
        var store = point == "beforeCreate" ? new ManifestOutageStore(inner, beforeCreate: DeleteAndRemoveAsync) : new ManifestOutageStore(inner, afterCopy: DeleteAndRemoveAsync);
        using var racing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IWorkspaceStore>(store)));

        await racing.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(factory.Sender.Sent.Single(sent => sent.Type == ActionType.CreateWorkspace), CancellationToken.None);

        Assert.Equal(0, store.AddCalls);
        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{child.ChildId:D}/"));
        Assert.False(await Family.GetBlobClient($"{family.FamilyId:D}/manifest.json").ExistsAsync());
    }

    [Fact]
    public async Task DeletingAChild_IsOneCallToTheStore_AndTheEntryIsGoneWithTheWorkspace()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3, 6);
        await factory.DeliverAsync();
        var (gone, kept) = (family.Children[0], family.Children[1]);
        var counting = new ManifestOutageStore(factory.Services.GetRequiredService<IWorkspaceStore>());
        using var counted = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IWorkspaceStore>(counting)));
        await client.DeleteAsync(ApiClientExtensions.Child(gone.ChildId));

        await counted.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(factory.Sender.Sent.Single(sent => sent.Type == ActionType.RemoveWorkspace), CancellationToken.None);

        Assert.Equal(1, counting.RemoveCalls);
        AssertJson(Manifest("cbse", [Ncert(kept.ChildId, 6)]), await ManifestAsync(family.FamilyId));
        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{gone.ChildId:D}/"));
    }

    public void Dispose() => factory.Dispose();

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

    private HttpClient Client(string uid) => factory.CreateClient().WithBearer(TestTokens.Create(uid));

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static string Locked(string plaintext) => $"v1.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext))}.{Guid.NewGuid():N}";

    private static ChildAddModel NewChild(Guid id, int classLevel) =>
        new() { ChildId = id, Name = Locked("Kavya"), ClassLevel = classLevel, BirthYear = Locked("2018"), School = Locked("Green School") };

    private static async Task<FamilyModel> RegisterAsync(HttpClient client, BoardType board, params int[] classes)
    {
        var body = new FamilyRegisterRequest
        {
            FamilyId = Guid.NewGuid(),
            Region = "Gujarat",
            Board = board,
            Language = "gu",
            ParentName = Locked("Meena Patel"),
            ParentEmail = Locked("meena@example.test"),
            RecoveryWrappedKey = Locked("recovery-key"),
            RecoverySalt = Locked("recovery-salt"),
            Consent = new ConsentModel { Accepted = true, NoticeVersion = "2026-09" },
            Children =
            [
                .. classes.Select(classLevel => new ChildSaveModel
                {
                    Name = Locked("Child"),
                    ClassLevel = classLevel,
                    BirthYear = Locked("2018"),
                    School = Locked("Sunrise School"),
                }),
            ],
        };

        var response = await client.RegisterAsync(body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<FamilyModel>())!;
    }

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
