using System.Net;
using System.Net.Http.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lantern.Api.Contracts;
using Lantern.Api.Repository;
using Lantern.Api.Services;
using Lantern.Api.Services.Interfaces;
using Lantern.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class ChildrenTests(AzuriteFixture azurite) : IDisposable
{
    private static readonly RowKeys Keys = new();

    private static readonly int Year = DateTime.UtcNow.Year;

    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    private BlobContainerClient Blobs => azurite.CreateBlobClient().GetBlobContainerClient("family");

    private TableClient Families => azurite.CreateClient().GetTableClient("families");

    private ChildDeletionWorker Worker => factory.Services.GetRequiredService<ChildDeletionWorker>();

    // ---- add ----

    [Fact]
    public async Task Add_Valid_Returns201_ListsItAfterTheOthersAndStartsItsClassSpace()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var id = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, "Kavya", 4, "Green School"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(ApiClientExtensions.Child(id), response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<ChildResponse>();
        Assert.Equal((id, "Kavya", "Green School", 4), (created!.ChildId, created.Name, created.School, created.ClassLevel));
        var after = await MeAsync(client);
        Assert.Equal(["Aarav", "Diya", "Kavya"], after.Children.Select(c => c.Name));
        Assert.True(await Blobs.GetBlobClient($"{me.FamilyId:D}/{id:D}/4/class.json").ExistsAsync());
    }

    [Fact]
    public async Task Add_SameChildIdTwice_Returns200AndAddsOne()
    {
        using var client = await RegisteredAsync();
        var id = Guid.NewGuid();
        await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, "Kavya"));

        var again = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, "Kavya"));

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(3, (await MeAsync(client)).Children.Count);
    }

    [Fact]
    public async Task Add_TwinsWithTheSameName_AreTwoChildren()
    {
        using var client = await RegisteredAsync();

        await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Aarav"));

        Assert.Equal(["Aarav", "Diya", "Aarav"], (await MeAsync(client)).Children.Select(c => c.Name));
    }

    [Fact]
    public async Task Add_SeventhChild_Returns409AndStartsNoClassSpace()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        foreach (var n in Enumerable.Range(0, 4))
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), $"C{n}"))).StatusCode);
        }
        var seventh = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(seventh, "Extra"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("child-limit-reached", await response.ProblemCodeAsync());
        Assert.Equal(6, (await MeAsync(client)).Children.Count);
        Assert.False(await Blobs.GetBlobClient($"{me.FamilyId:D}/{seventh:D}/1/class.json").ExistsAsync());
    }

    [Fact]
    public async Task Add_TenAtOnceAtFive_NeverPassesSix()
    {
        using var client = await RegisteredAsync();
        for (var n = 0; n < 3; n++)
        {
            await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), $"C{n}"));
        }

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(n => client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), $"R{n}")))
        );

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(6, (await MeAsync(client)).Children.Count);
    }

    [Theory]
    [InlineData("class-zero")]
    [InlineData("class-eleven")]
    [InlineData("born-too-long-ago")]
    [InlineData("born-too-recently")]
    [InlineData("empty-name")]
    [InlineData("overlong-name")]
    [InlineData("control-character-in-name")]
    [InlineData("overlong-school")]
    [InlineData("control-character-in-school")]
    [InlineData("empty-child-id")]
    public async Task Add_InvalidBody_Returns400AndStoresNothing(string scenario)
    {
        using var client = await RegisteredAsync();
        var body = NewChild(Guid.NewGuid(), "Kavya");
        switch (scenario)
        {
            case "class-zero": body.ClassLevel = 0; break;
            case "class-eleven": body.ClassLevel = 11; break;
            case "born-too-long-ago": body.BirthYear = Year - 19; break;
            case "born-too-recently": body.BirthYear = Year - 2; break;
            case "empty-name": body.Name = " "; break;
            case "overlong-name": body.Name = new string('x', 41); break;
            case "control-character-in-name": body.Name = "Kav\u0007ya"; break;
            case "overlong-school": body.School = new string('x', 121); break;
            case "control-character-in-school": body.School = "Gre\nen"; break;
            case "empty-child-id": body.ChildId = Guid.Empty; break;
        }

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, (await MeAsync(client)).Children.Count);
    }

    [Fact]
    public async Task Add_WithoutToken_Returns401_AndUnregistered_Returns404()
    {
        using var anonymous = factory.CreateClient();
        using var unregistered = factory.CreateClient().WithBearer(TestTokens.Create(NewUid()));

        var denied = await anonymous.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Kavya"));
        var missing = await unregistered.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Kavya"));

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not-registered", await missing.ProblemCodeAsync());
    }

    [Fact]
    public async Task Add_WhenTheClassSpaceFails_AddsNoChild()
    {
        var classSpaces = new Mock<IClassSpaceStore>();
        using var client = await RegisteredAsync();
        classSpaces
            .Setup(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), 9, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, "blob down"));
        using var failing = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(classSpaces.Object)));
        using var sameCaller = failing.CreateClient().WithBearer(client.DefaultRequestHeaders.Authorization!.Parameter!);

        var response = await sameCaller.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Kavya", 9));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(2, (await MeAsync(client)).Children.Count);
    }

    [Fact]
    public async Task Add_StoredRow_HoldsNoPlaintext()
    {
        using var client = await RegisteredAsync();
        var name = $"Secret-{Guid.NewGuid():N}";
        var school = $"School-{Guid.NewGuid():N}";
        var id = Guid.NewGuid();

        await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, name, 3, school));

        var row = await Families.GetEntityAsync<TableEntity>(
            Keys.FamilyPartition((await MeAsync(client)).FamilyId),
            Keys.ChildRowKey(id)
        );
        Assert.DoesNotContain(name, Flat(row.Value), StringComparison.Ordinal);
        Assert.DoesNotContain(school, Flat(row.Value), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Add_ChildIdOfAChildBeingDeleted_Returns409()
    {
        using var client = await RegisteredAsync();
        var first = (await MeAsync(client)).Children[0];
        await client.DeleteAsync(ApiClientExtensions.Child(first.ChildId));

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(first.ChildId, "Again"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("child-deleting", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Add_AfterADelete_FreesTheSlotAtOnce()
    {
        using var client = await RegisteredAsync();
        foreach (var n in Enumerable.Range(0, 4))
        {
            await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), $"C{n}"));
        }
        await client.DeleteAsync(ApiClientExtensions.Child((await MeAsync(client)).Children[0].ChildId));

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Fresh"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Fresh", (await MeAsync(client)).Children[^1].Name);
    }

    // ---- edit ----

    [Fact]
    public async Task Edit_ChangesNameSchoolAndBirthYear_NotTheClass()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0];

        var response = await client.PutAsJsonAsync(
            ApiClientExtensions.Child(child.ChildId),
            new { Name = "Aarav K", School = "New School", BirthYear = Year - 7, ClassLevel = 9 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var now = (await MeAsync(client)).Children[0];
        Assert.Equal(("Aarav K", "New School", Year - 7, child.ClassLevel), (now.Name, now.School, now.BirthYear, now.ClassLevel));
    }

    [Fact]
    public async Task Edit_WithNoSchool_ClearsIt_AndStoresNoPlaintext()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var child = me.Children[0];
        var name = $"Renamed-{Guid.NewGuid():N}";

        await client.PutAsJsonAsync(ApiClientExtensions.Child(child.ChildId), new { Name = name, School = "  ", BirthYear = child.BirthYear });

        Assert.Null((await MeAsync(client)).Children[0].School);
        var row = await Families.GetEntityAsync<TableEntity>(Keys.FamilyPartition(me.FamilyId), Keys.ChildRowKey(child.ChildId));
        Assert.False(row.Value.ContainsKey("SchoolCipher"));
        Assert.DoesNotContain(name, Flat(row.Value), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_AnotherFamilysChild_Returns404AndChangesNothing()
    {
        using var mine = await RegisteredAsync();
        using var theirs = await RegisteredAsync();
        var theirChild = (await MeAsync(theirs)).Children[0];

        var response = await mine.PutAsJsonAsync(
            ApiClientExtensions.Child(theirChild.ChildId),
            new { Name = "Hijacked", BirthYear = theirChild.BirthYear }
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("child-not-found", await response.ProblemCodeAsync());
        Assert.Equal("Aarav", (await MeAsync(theirs)).Children[0].Name);
    }

    [Fact]
    public async Task Edit_UnknownDeletingOrInvalid_IsRefused()
    {
        using var client = await RegisteredAsync();
        var children = (await MeAsync(client)).Children;
        await client.DeleteAsync(ApiClientExtensions.Child(children[1].ChildId));

        var unknown = await client.PutAsJsonAsync(ApiClientExtensions.Child(Guid.NewGuid()), new { Name = "X", BirthYear = Year - 6 });
        var deleting = await client.PutAsJsonAsync(ApiClientExtensions.Child(children[1].ChildId), new { Name = "X", BirthYear = Year - 6 });
        var badYear = await client.PutAsJsonAsync(ApiClientExtensions.Child(children[0].ChildId), new { Name = "X", BirthYear = Year - 30 });
        var badName = await client.PutAsJsonAsync(ApiClientExtensions.Child(children[0].ChildId), new { Name = " ", BirthYear = Year - 6 });

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleting.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badYear.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badName.StatusCode);
        Assert.Equal("Aarav", (await MeAsync(client)).Children[0].Name);
    }

    [Fact]
    public async Task Edit_WithoutToken_Returns401_AndUnregistered_Returns404()
    {
        using var anonymous = factory.CreateClient();
        using var unregistered = factory.CreateClient().WithBearer(TestTokens.Create(NewUid()));
        var body = new { Name = "X", BirthYear = Year - 6 };

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(ApiClientExtensions.Child(Guid.NewGuid()), body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unregistered.PutAsJsonAsync(ApiClientExtensions.Child(Guid.NewGuid()), body)).StatusCode);
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_Returns202_HidesTheChildAtOnce_AndTheWorkerRemovesEverythingOfItAndNothingElse()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var (gone, kept) = (me.Children[0], me.Children[1]);
        // The Child moved from Class 5 to Class 6 (issue #50 will do this): two Class spaces.
        await new BlobClassSpaceStore(Blobs, createContainer: false).StartAsync(me.FamilyId, gone.ChildId, 6, CancellationToken.None);

        var response = await client.DeleteAsync(ApiClientExtensions.Child(gone.ChildId));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal([kept.ChildId], (await MeAsync(client)).Children.Select(c => c.ChildId));
        Assert.True(await Blobs.GetBlobClient($"{me.FamilyId:D}/{gone.ChildId:D}/{gone.ClassLevel}/class.json").ExistsAsync());

        await Worker.RunOnceAsync(CancellationToken.None);

        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/{gone.ChildId:D}/"));
        Assert.True(await Blobs.GetBlobClient($"{me.FamilyId:D}/{kept.ChildId:D}/{kept.ClassLevel}/class.json").ExistsAsync());
        Assert.Empty(await PendingAsync(me.FamilyId));
        Assert.Empty(await Families.QueryAsync<TableEntity>(r => r.PartitionKey == Keys.FamilyPartition(me.FamilyId) && r.RowKey == Keys.ChildRowKey(gone.ChildId)).ToListAsync());
    }

    [Fact]
    public async Task Delete_Repeated_Returns202_BeforeAndAfterTheWorkerRuns()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0];

        var first = await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));
        var second = await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));
        await Worker.RunOnceAsync(CancellationToken.None);
        var third = await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, third.StatusCode);
    }

    [Fact]
    public async Task Delete_AnotherFamilysChild_Returns404AndTouchesNothingOfIt()
    {
        using var mine = await RegisteredAsync();
        using var theirs = await RegisteredAsync();
        var theirFamily = await MeAsync(theirs);
        var theirChild = theirFamily.Children[0];

        var response = await mine.DeleteAsync(ApiClientExtensions.Child(theirChild.ChildId));
        await Worker.RunOnceAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("child-not-found", await response.ProblemCodeAsync());
        Assert.Equal(2, (await MeAsync(theirs)).Children.Count);
        Assert.NotEmpty(await BlobNamesAsync($"{theirFamily.FamilyId:D}/{theirChild.ChildId:D}/"));
        Assert.Empty(await PendingAsync(theirFamily.FamilyId));
        var row = await Families.GetEntityAsync<TableEntity>(Keys.FamilyPartition(theirFamily.FamilyId), Keys.ChildRowKey(theirChild.ChildId));
        Assert.Equal("Active", row.Value.GetString("Status"));
    }

    [Fact]
    public async Task Delete_UnknownChild_UnregisteredCaller_AndNoToken_AreRefused()
    {
        using var client = await RegisteredAsync();
        using var unregistered = factory.CreateClient().WithBearer(TestTokens.Create(NewUid()));
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(ApiClientExtensions.Child(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unregistered.DeleteAsync(ApiClientExtensions.Child(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(ApiClientExtensions.Child(Guid.NewGuid()))).StatusCode);
        Assert.Equal(2, (await MeAsync(client)).Children.Count);
    }

    [Fact]
    public async Task Delete_EveryChild_LeavesAnEmptyFamily_ThatCanAddAgain()
    {
        using var client = await RegisteredAsync();
        foreach (var child in (await MeAsync(client)).Children)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId))).StatusCode);
        }
        await Worker.RunOnceAsync(CancellationToken.None);

        Assert.Empty((await MeAsync(client)).Children);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Fresh"))).StatusCode);
    }

    [Fact]
    public async Task Worker_WhenTheBlobStepFails_KeepsTheRowAndPendingJob_AndFinishesOnTheNextPass()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var child = me.Children[0];
        await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));
        var failing = new Mock<IClassSpaceStore>();
        failing
            .Setup(s => s.DeleteChildAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "blob down"));
        using var broken = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(failing.Object)));

        await broken.Services.GetRequiredService<ChildDeletionWorker>().RunOnceAsync(CancellationToken.None);

        Assert.Single(await PendingAsync(me.FamilyId));
        Assert.True(await Families.GetEntityIfExistsAsync<TableEntity>(Keys.FamilyPartition(me.FamilyId), Keys.ChildRowKey(child.ChildId)) is { HasValue: true });
        Assert.NotEmpty(await BlobNamesAsync($"{me.FamilyId:D}/{child.ChildId:D}/"));
        Assert.Single((await MeAsync(client)).Children);

        await MakeDueAsync(me.FamilyId);
        await Worker.RunOnceAsync(CancellationToken.None);

        Assert.Empty(await PendingAsync(me.FamilyId));
        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/{child.ChildId:D}/"));
    }

    [Fact]
    public async Task Worker_ResumesAfterACrashBetweenTheRowDeleteAndTheLastStep()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var child = me.Children[0];
        await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));
        await Blobs.DeleteBlobIfExistsAsync($"{me.FamilyId:D}/{child.ChildId:D}/{child.ClassLevel}/class.json");
        await Families.DeleteEntityAsync(Keys.FamilyPartition(me.FamilyId), Keys.ChildRowKey(child.ChildId));

        await Worker.RunOnceAsync(CancellationToken.None);

        Assert.Empty(await PendingAsync(me.FamilyId));
    }

    [Fact]
    public async Task Worker_TwoPassesAtOnce_FinishTheJobWithoutError()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        await client.DeleteAsync(ApiClientExtensions.Child(me.Children[0].ChildId));
        await client.DeleteAsync(ApiClientExtensions.Child(me.Children[1].ChildId));

        await Task.WhenAll(Worker.RunOnceAsync(CancellationToken.None), Worker.RunOnceAsync(CancellationToken.None));

        Assert.Empty(await PendingAsync(me.FamilyId));
        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/"));
    }

    [Fact]
    public async Task Worker_OnlyReachesPathsOfTheJobsOwnFamilyAndChild_AndNeverTheAnswerLibrary()
    {
        using var client = await RegisteredAsync();
        using var other = await RegisteredAsync();
        var me = await MeAsync(client);
        var otherFamily = await MeAsync(other);
        await Blobs.GetBlobClient("answers/stage-3/q1.json").UploadAsync(BinaryData.FromString("{}"), overwrite: true);
        await Families.UpsertEntityAsync(new TableEntity("answer-library", "q1") { ["Text"] = "scrubbed" });
        // A job naming my Family with a Child of the other Family reaches nothing of theirs.
        await Families.UpsertEntityAsync(new TableEntity(ChildDeletionStore.PendingPartition, "forged")
        {
            ["FamilyId"] = me.FamilyId,
            ["ChildId"] = otherFamily.Children[0].ChildId,
            ["Attempts"] = 0,
            ["LeaseUntil"] = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await client.DeleteAsync(ApiClientExtensions.Child(me.Children[0].ChildId));

        await Worker.RunOnceAsync(CancellationToken.None);

        Assert.NotEmpty(await BlobNamesAsync($"{otherFamily.FamilyId:D}/{otherFamily.Children[0].ChildId:D}/"));
        Assert.True(await Blobs.GetBlobClient("answers/stage-3/q1.json").ExistsAsync());
        Assert.True((await Families.GetEntityIfExistsAsync<TableEntity>("answer-library", "q1")).HasValue);
        Assert.Equal(2, (await MeAsync(other)).Children.Count);
    }

    [Fact]
    public async Task TheRealWorkerLoop_FinishesADeleteOnItsOwn()
    {
        using var looping = new LanternApiFactory(azurite.ConnectionString, runDeletionWorker: true);
        using var client = looping.CreateClient().WithBearer(TestTokens.Create(NewUid()));
        await client.RegisterAsync(RegisterBody());
        var me = await client.GetFromJsonAsync<FamilyResponse>(ApiClientExtensions.Me);
        var child = me!.Children[0];

        await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && (await BlobNamesAsync($"{me.FamilyId:D}/{child.ChildId:D}/")).Count > 0)
        {
            await Task.Delay(250);
        }

        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/{child.ChildId:D}/"));
        Assert.Empty(await PendingAsync(me.FamilyId));
    }

    // ---- cross-cutting ----

    [Fact]
    public async Task Add_OversizedBody_IsRefusedBeforeItIsRead()
    {
        using var client = await RegisteredAsync();
        var body = NewChild(Guid.NewGuid(), "Kavya", 1, new string('x', 5000));

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, body);

        Assert.True(response.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest);
        Assert.Equal(2, (await MeAsync(client)).Children.Count);
    }


    [Fact]
    public async Task Children_PastThePerMinuteLimit_Get429_ForThatCallerOnly()
    {
        using var noisy = await RegisteredAsync();
        using var quiet = await RegisteredAsync();

        HttpStatusCode last = default;
        for (var n = 0; n < 40; n++)
        {
            last = (await noisy.DeleteAsync(ApiClientExtensions.Child(Guid.NewGuid()))).StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
        Assert.Equal(HttpStatusCode.NotFound, (await quiet.DeleteAsync(ApiClientExtensions.Child(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task Requests_NeverLogChildNamesOrSchools()
    {
        using var client = await RegisteredAsync();
        var name = $"Child-{Guid.NewGuid():N}";
        var school = $"School-{Guid.NewGuid():N}";
        var id = Guid.NewGuid();

        await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, name, 2, school));
        await client.PutAsJsonAsync(ApiClientExtensions.Child(id), new { Name = name + "x", School = school + "x", BirthYear = Year - 6 });
        await client.DeleteAsync(ApiClientExtensions.Child(id));
        await Worker.RunOnceAsync(CancellationToken.None);

        Assert.NotEmpty(factory.Logs.Entries);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Contains(name, StringComparison.Ordinal) || e.Contains(school, StringComparison.Ordinal));
    }

    // ---- helpers ----

    private async Task<HttpClient> RegisteredAsync()
    {
        var client = factory.CreateClient().WithBearer(TestTokens.Create(NewUid()));
        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(RegisterBody())).StatusCode);

        return client;
    }

    private static async Task<FamilyResponse> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<FamilyResponse>(ApiClientExtensions.Me))!;

    private static string Flat(TableEntity row) => string.Join('|', ((IDictionary<string, object>)row).Values);

    private async Task<List<string>> BlobNamesAsync(string prefix) =>
        await Blobs
            .GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None)
            .Select(blob => blob.Name)
            .ToListAsync();

    private async Task<List<TableEntity>> PendingAsync(Guid familyId) =>
        await Families
            .QueryAsync<TableEntity>(r => r.PartitionKey == ChildDeletionStore.PendingPartition && r.RowKey.CompareTo($"{familyId:N}_") >= 0 && r.RowKey.CompareTo($"{familyId:N}`") < 0)
            .ToListAsync();

    private async Task MakeDueAsync(Guid familyId)
    {
        foreach (var row in await PendingAsync(familyId))
        {
            row["LeaseUntil"] = DateTimeOffset.UtcNow.AddMinutes(-1);
            await Families.UpdateEntityAsync(row, row.ETag, TableUpdateMode.Merge);
        }
    }

    private static AddChildBody NewChild(Guid id, string name, int classLevel = 1, string? school = null) =>
        new() { ChildId = id, Name = name, ClassLevel = classLevel, BirthYear = Year - 8, School = school };

    private static RegisterBody RegisterBody() =>
        new()
        {
            Region = "Gujarat",
            Language = "gu",
            Consent = new ConsentBody { Accepted = true, NoticeVersion = "2026-09" },
            Children =
            [
                new ChildBody { Name = "Aarav", ClassLevel = 5, BirthYear = Year - 10 },
                new ChildBody { Name = "Diya", ClassLevel = 3, BirthYear = Year - 8 },
            ],
        };

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    public void Dispose() => factory.Dispose();
}
