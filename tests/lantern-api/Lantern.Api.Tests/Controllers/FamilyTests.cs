extern alias Functions;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Functions::Lantern.Functions.Handler;
using Lantern.Api.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Actions;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class FamilyTests(AzuriteFixture azurite) : IDisposable
{
    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    private BlobContainerClient Blobs => azurite.CreateBlobClient().GetBlobContainerClient("family");

    private TableClient Families => azurite.CreateClient().GetTableClient("families");

    private TableClient Parents => azurite.CreateClient().GetTableClient("parents");

    private TableClient Actions => azurite.CreateClient().GetTableClient("actions");

    private IReadOnlyList<ActionMessage> Erasures => [.. factory.Sender.Sent.Where(m => m.Type == ActionType.RemoveFamily)];

    [Fact]
    public async Task Register_Valid_Returns201AndMeReturnsTheSameFamilyWithTheSameLockedValues()
    {
        var uid = NewUid();
        using var client = Client(uid, "Meena Patel", "meena@example.test");
        var body = ValidBody();

        var created = await client.RegisterAsync(body);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var family = await created.Content.ReadFromJsonAsync<FamilyModel>();
        Assert.NotNull(family);
        Assert.Equal(body.FamilyId, family.FamilyId);
        Assert.NotEqual(Guid.Empty, family.Parent.ParentId);
        Assert.Equal(body.ParentNameLocked, family.Parent.NameLocked);
        Assert.Equal(body.ParentEmailLocked, family.Parent.EmailLocked);

        var me = await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);
        Assert.NotNull(me);
        Assert.Equal(body.FamilyId, me.FamilyId);
        Assert.Equal("Gujarat", me.Region);
        Assert.Equal(BoardType.Ssc, me.Board);
        Assert.Equal(family.Parent.ParentId, me.Parent.ParentId);
        Assert.Equal("gu", me.Parent.Language);
        Assert.Equal("2026-09", me.Parent.ConsentVersion);
        Assert.Equal(body.ParentNameLocked, me.Parent.NameLocked);
        Assert.Equal(body.Children.Select(child => child.NameLocked), me.Children.Select(child => child.NameLocked));
        Assert.Equal(body.Children.Select(child => child.BirthYearLocked), me.Children.Select(child => child.BirthYearLocked));
        Assert.Equal(body.Children[0].SchoolLocked, me.Children[0].SchoolLocked);
        Assert.Null(me.Children[1].SchoolLocked);
    }

    [Fact]
    public async Task Me_ReturnsTheWrappedKeysTheCallersFamilyWasRegisteredWith_AndNeverAnotherFamilys()
    {
        using var first = Client(NewUid());
        using var second = Client(NewUid());
        var firstBody = ValidBody();
        var secondBody = ValidBody();
        secondBody.PassphraseWrappedKey = Locked("other-passphrase-key");
        secondBody.RecoveryWrappedKey = Locked("other-recovery-key");
        await first.RegisterAsync(firstBody);
        await second.RegisterAsync(secondBody);

        var meFirst = await first.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);
        var meSecond = await second.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);

        Assert.Equal(firstBody.PassphraseWrappedKey, meFirst!.PassphraseWrappedKey);
        Assert.Equal(firstBody.PassphraseSalt, meFirst.PassphraseSalt);
        Assert.Equal(firstBody.RecoveryWrappedKey, meFirst.RecoveryWrappedKey);
        Assert.Equal(firstBody.RecoverySalt, meFirst.RecoverySalt);
        Assert.Equal(secondBody.PassphraseWrappedKey, meSecond!.PassphraseWrappedKey);
        Assert.NotEqual(meFirst.PassphraseWrappedKey, meSecond.PassphraseWrappedKey);
        Assert.NotEqual(meFirst.RecoveryWrappedKey, meSecond.RecoveryWrappedKey);
    }

    [Fact]
    public async Task Register_AFamilyIdThatAlreadyExists_Returns409FamilyIdTaken_AndChangesNothing()
    {
        using var first = Client(NewUid());
        using var second = Client(NewUid());
        var firstBody = ValidBody();
        await first.RegisterAsync(firstBody);
        var stolen = ValidBody();
        stolen.FamilyId = firstBody.FamilyId;

        var response = await second.RegisterAsync(stolen);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("family-id-taken", await response.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync(ApiClientExtensions.Me)).StatusCode);
        var me = await first.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);
        Assert.Equal(firstBody.PassphraseWrappedKey, me!.PassphraseWrappedKey);
        Assert.Equal(firstBody.Children.Count, me.Children.Count);
    }

    [Fact]
    public async Task Register_TwoCallersWithTheSameFamilyIdAtOnce_ExactlyOneWins()
    {
        var id = Guid.NewGuid();
        var responses = await Task.WhenAll(
            Enumerable
                .Range(0, 6)
                .Select(_ =>
                {
                    var body = ValidBody();
                    body.FamilyId = id;
                    return factory.CreateClient().WithBearer(TestTokens.Create(NewUid())).RegisterAsync(body);
                })
        );

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
    }

    [Theory]
    [InlineData("cbse", 10)]
    [InlineData("ssc", 5)]
    public async Task Register_BothBoards_AreStoredAndReturnedByWireName(string board, int topClass)
    {
        using var client = Client(NewUid());
        var body = ValidBody();
        body.Board = board == "cbse" ? BoardType.Cbse : BoardType.Ssc;
        body.Children[1].ClassLevel = topClass;

        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(body)).StatusCode);

        using var me = JsonDocument.Parse(await client.GetStringAsync(ApiClientExtensions.Me));
        Assert.Equal(board, me.RootElement.GetProperty("board").GetString());
    }

    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    public async Task Register_AnSscChildAboveClassFive_Returns400ClassNotAvailable_AndStoresNothing(int classLevel)
    {
        using var client = Client(NewUid());
        var body = ValidBody();
        body.Children[1].ClassLevel = classLevel;

        var response = await client.RegisterAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("class-not-available", await response.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ApiClientExtensions.Me)).StatusCode);
    }

    [Fact]
    public async Task Register_Twice_Returns409AlreadyRegistered()
    {
        using var client = Client(NewUid());
        await client.RegisterAsync(ValidBody());

        var again = await client.RegisterAsync(ValidBody());

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already-registered", await again.ProblemCodeAsync());
    }

    [Fact]
    public async Task Register_TenAtOnce_ExactlyOneCreated()
    {
        var token = TestTokens.Create(NewUid());

        var responses = await Task.WhenAll(
            Enumerable
                .Range(0, 10)
                .Select(_ => factory.CreateClient().WithBearer(token).RegisterAsync(ValidBody()))
        );

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Me_BeforeRegister_Returns404NotRegistered()
    {
        using var client = Client(NewUid());

        var response = await client.GetAsync(ApiClientExtensions.Me);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not-registered", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Me_AnotherMother_NeverSeesTheFirstMothersFamily()
    {
        using var first = Client(NewUid());
        await first.RegisterAsync(ValidBody());
        using var second = Client(NewUid());

        var response = await second.GetAsync(ApiClientExtensions.Me);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("consent-declined")]
    [InlineData("no-children")]
    [InlineData("seven-children")]
    [InlineData("class-zero")]
    [InlineData("class-eleven")]
    [InlineData("board-missing")]
    [InlineData("family-id-missing")]
    [InlineData("passphrase-key-missing")]
    [InlineData("recovery-salt-missing")]
    [InlineData("parent-name-missing")]
    [InlineData("language-not-offered")]
    [InlineData("empty-region")]
    [InlineData("overlong-region")]
    [InlineData("empty-child-name")]
    [InlineData("overlong-child-name")]
    [InlineData("empty-birth-year")]
    [InlineData("missing-consent-version")]
    public async Task Register_InvalidBody_Returns400AndStoresNothing(string scenario)
    {
        using var client = Client(NewUid());
        var body = ValidBody();
        Mutate(scenario, body);

        var response = await client.RegisterAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ApiClientExtensions.Me)).StatusCode);
    }

    [Fact]
    public async Task Register_StoredRows_HoldExactlyTheLockedValuesTheClientSent_AndNoUidOrTokenDetail()
    {
        var uid = NewUid();
        var name = $"Name-{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid():N}@example.test";
        using var client = Client(uid, name, email);
        var body = ValidBody();
        await client.RegisterAsync(body);

        var stored = new List<string>();
        foreach (var tableName in new[] { "parents", "families" })
        {
            var table = new TableServiceClient(azurite.ConnectionString).GetTableClient(tableName);
            await foreach (var entity in table.QueryAsync<TableEntity>())
            {
                stored.Add(entity.PartitionKey);
                stored.Add(entity.RowKey);
                stored.AddRange(entity.Select(pair => pair.Value).OfType<string>());
            }
        }

        foreach (var sent in new[]
        {
            body.ParentNameLocked, body.ParentEmailLocked, body.PassphraseWrappedKey, body.PassphraseSalt,
            body.RecoveryWrappedKey, body.RecoverySalt, body.Children[0].NameLocked, body.Children[0].BirthYearLocked,
            body.Children[0].SchoolLocked!,
        })
        {
            Assert.Contains(sent, stored);
        }

        foreach (var secret in new[] { uid, name, email })
        {
            Assert.DoesNotContain(stored, value => value.Contains(secret, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Register_StartsAWorkspaceForEachChildAtTheirClass()
    {
        using var client = Client(NewUid());

        var response = await client.RegisterAsync(ValidBody());

        var family = await response.Content.ReadFromJsonAsync<FamilyModel>();
        Assert.NotNull(family);
        await factory.DeliverAsync();
        var container = azurite.CreateBlobClient().GetBlobContainerClient(LanternApiFactory.WorkspaceContainer);
        foreach (var child in family.Children)
        {
            var marker = container.GetBlobClient($"{family.FamilyId:D}/{child.ChildId:D}/{child.ClassLevel}/class.json");
            Assert.True(await marker.ExistsAsync());
        }
    }

    [Fact]
    public async Task Register_Twice_StartsNoWorkspacesTheSecondTime()
    {
        using var client = Client(NewUid());
        await client.RegisterAsync(ValidBody());

        var again = await client.RegisterAsync(ValidBody());

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ValidBody().Children.Count, factory.Sender.Sent.Count(m => m.Type == ActionType.CreateWorkspace));
    }

    [Fact]
    public async Task Register_WhenTheQueueIsDown_StillRegisters()
    {
        factory.Sender.Down = true;
        using var client = Client(NewUid());

        var registered = await client.RegisterAsync(ValidBody());

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.Empty(factory.Sender.Sent);
    }

    [Fact]
    public async Task Register_WhenStorageFails_IsAHardStopThatNeverLeaksTheDetail()
    {
        var repository = new Mock<IFamilyRepository>();
        repository
            .Setup(r => r.RegisterAsync(It.IsAny<string>(), It.IsAny<Family>(), It.IsAny<Parent>(), It.IsAny<IReadOnlyList<Child>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, "secret-detail-account-name"));
        using var failing = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(repository.Object))
        );
        using var client = failing.CreateClient().WithBearer(TestTokens.Create(NewUid()));

        var response = await client.RegisterAsync(ValidBody());

        // Storage failures aren't a known, mapped outcome: they're a hard stop (a real 500), not a
        // glossed-over "service unavailable, try again" that pretends to know what happened.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("secret-detail", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Requests_NeverLogUidEmailNameOrToken()
    {
        var uid = NewUid();
        var name = $"Name-{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid():N}@example.test";
        var token = TestTokens.Create(uid, name, email);
        using var client = factory.CreateClient().WithBearer(token);
        var body = ValidBody();
        var childName = body.Children[0].NameLocked;

        await client.RegisterAsync(body);
        await client.RegisterAsync(body);
        await client.GetAsync(ApiClientExtensions.Me);
        using var refused = factory.CreateClient().WithBearer(TestTokens.Create(uid, key: TestTokens.OtherKey));
        await refused.GetAsync(ApiClientExtensions.Me);

        var logged = factory.Logs.Entries;
        Assert.NotEmpty(logged);
        foreach (var secret in new[] { uid, name, email, childName, token })
        {
            Assert.DoesNotContain(logged, entry => entry.Contains(secret, StringComparison.Ordinal));
        }
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_Returns204_LocksTheCallerOutAtOnce_AndTheHandlerErasesEverythingOfItAndNothingElse()
    {
        using var client = await RegisteredAsync();
        using var other = await RegisteredAsync();
        var me = await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);
        var theirs = await other.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);

        var response = await client.DeleteAsync(ApiClientExtensions.Family);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("not-registered", await (await client.GetAsync(ApiClientExtensions.Me)).ProblemCodeAsync());
        Assert.Equal("not-registered", await (await client.DeleteAsync(ApiClientExtensions.Child(me!.Children[0].ChildId))).ProblemCodeAsync());
        Assert.Empty(await ProfilesAsync(me.FamilyId));
        Assert.Single(Erasures);
        Assert.Single(await LedgerAsync(me.FamilyId));

        await factory.DeliverAsync();

        Assert.Empty(await RowsAsync(me.FamilyId));
        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/"));
        Assert.Empty(await LedgerAsync(me.FamilyId));
        Assert.Equal(2, (await other.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me))!.Children.Count);
        Assert.NotEmpty(await BlobNamesAsync($"{theirs!.FamilyId:D}/"));
    }

    [Fact]
    public async Task Delete_ThenRegisterBeforeTheHandlerRuns_StartsANewFamily_ThatTheErasureLeavesAlone()
    {
        using var client = await RegisteredAsync();
        var old = await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);

        await client.DeleteAsync(ApiClientExtensions.Family);
        var again = await client.RegisterAsync(ValidBody());
        await factory.DeliverAsync();

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var now = await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);
        Assert.NotEqual(old!.FamilyId, now!.FamilyId);
        Assert.Equal(2, now.Children.Count);
        Assert.Empty(await RowsAsync(old.FamilyId));
    }

    [Fact]
    public async Task Delete_Repeated_UnregisteredCaller_AndNoToken_AreRefused()
    {
        using var client = await RegisteredAsync();
        using var anonymous = factory.CreateClient();

        await client.DeleteAsync(ApiClientExtensions.Family);

        Assert.Equal("not-registered", await (await client.DeleteAsync(ApiClientExtensions.Family)).ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(ApiClientExtensions.Family)).StatusCode);
        Assert.Single(Erasures);
    }

    [Fact]
    public async Task Handler_WhenTheBlobStepFails_KeepsTheLedger_AndFinishesWhenTheMessageIsDeliveredAgain()
    {
        using var client = await RegisteredAsync();
        var me = await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);
        await client.DeleteAsync(ApiClientExtensions.Family);
        var failing = new Mock<IWorkspaceStore>();
        failing
            .Setup(s => s.RemoveFamilyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "blob down"));
        using var broken = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(failing.Object)));

        await Assert.ThrowsAsync<RequestFailedException>(() =>
            broken.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(Erasures[0], CancellationToken.None)
        );

        Assert.Single(await LedgerAsync(me!.FamilyId));
        Assert.NotEmpty(await BlobNamesAsync($"{me.FamilyId:D}/"));

        await factory.DeliverAsync();

        Assert.Empty(await LedgerAsync(me.FamilyId));
        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/"));
        Assert.Empty(await RowsAsync(me.FamilyId));
    }

    [Fact]
    public async Task Handler_TheSameMessageTwiceAtOnce_FinishesWithoutError()
    {
        using var client = await RegisteredAsync();
        var me = await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);
        await client.DeleteAsync(ApiClientExtensions.Family);
        var dispatcher = factory.Services.GetRequiredService<IActionDispatcher>();

        await Task.WhenAll(
            dispatcher.DispatchAsync(Erasures[0], CancellationToken.None),
            dispatcher.DispatchAsync(Erasures[0], CancellationToken.None)
        );

        Assert.Empty(await LedgerAsync(me!.FamilyId));
        Assert.Empty(await RowsAsync(me.FamilyId));
    }

    [Fact]
    public async Task Handler_AMessageWithNoLedgerRow_TouchesNothing()
    {
        using var client = await RegisteredAsync();
        var me = await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);
        var stray = new ActionMessage(
            "stray",
            ActionType.RemoveFamily,
            JsonSerializer.Serialize(new RemoveFamilyPayload(me!.FamilyId), JsonSerializerOptions.Web)
        );

        await factory.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(stray, CancellationToken.None);

        Assert.Equal(2, (await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me))!.Children.Count);
        Assert.NotEmpty(await BlobNamesAsync($"{me.FamilyId:D}/"));
    }

    private HttpClient Client(string uid, string? name = null, string? email = null) =>
        factory.CreateClient().WithBearer(TestTokens.Create(uid, name, email));

    private async Task<HttpClient> RegisteredAsync()
    {
        var client = Client(NewUid());
        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(ValidBody())).StatusCode);
        await factory.DeliverAsync();

        return client;
    }

    private async Task<List<TableEntity>> RowsAsync(Guid familyId) =>
        await Families.QueryAsync<TableEntity>(row => row.PartitionKey == familyId.ToString()).ToListAsync();

    private async Task<List<TableEntity>> ProfilesAsync(Guid familyId) =>
        await Parents.QueryAsync<TableEntity>(row => row.GetGuid("FamilyId") == familyId).ToListAsync();

    private async Task<List<TableEntity>> LedgerAsync(Guid familyId) =>
        await Actions.QueryAsync<TableEntity>(row => row.PartitionKey == nameof(ActionType.RemoveFamily) && row.RowKey == familyId.ToString("N")).ToListAsync();

    private async Task<List<string>> BlobNamesAsync(string prefix) =>
        await Blobs.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None).Select(blob => blob.Name).ToListAsync();

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    // What the phone sends: every personal value already locked. The tests only need them to be distinct and opaque.
    private static string Locked(string plaintext) => $"v1.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext))}.{Guid.NewGuid():N}";

    private static FamilyRegisterRequest ValidBody() =>
        new()
        {
            FamilyId = Guid.NewGuid(),
            Region = "Gujarat",
            Board = BoardType.Ssc,
            Language = "gu",
            ParentNameLocked = Locked("Meena Patel"),
            ParentEmailLocked = Locked("meena@example.test"),
            PassphraseWrappedKey = Locked("passphrase-key"),
            PassphraseSalt = Locked("passphrase-salt"),
            RecoveryWrappedKey = Locked("recovery-key"),
            RecoverySalt = Locked("recovery-salt"),
            Consent = new ConsentModel { Accepted = true, NoticeVersion = "2026-09" },
            Children =
            [
                new ChildSaveModel { NameLocked = Locked("Aarav"), ClassLevel = 1, BirthYearLocked = Locked("2020"), SchoolLocked = Locked("Sunrise School") },
                new ChildSaveModel { NameLocked = Locked("Diya"), ClassLevel = 3, BirthYearLocked = Locked("2018") },
            ],
        };

    private static void Mutate(string scenario, FamilyRegisterRequest request)
    {
        switch (scenario)
        {
            case "consent-declined": request.Consent.Accepted = false; break;
            case "no-children": request.Children = []; break;
            case "seven-children": request.Children = [.. Enumerable.Repeat(request.Children[0], 7)]; break;
            case "class-zero": request.Children[0].ClassLevel = 0; break;
            case "class-eleven": request.Children[0].ClassLevel = 11; break;
            case "board-missing": request.Board = null; break;
            case "family-id-missing": request.FamilyId = null; break;
            case "passphrase-key-missing": request.PassphraseWrappedKey = ""; break;
            case "recovery-salt-missing": request.RecoverySalt = ""; break;
            case "parent-name-missing": request.ParentNameLocked = ""; break;
            case "language-not-offered": request.Language = "fr"; break;
            case "empty-region": request.Region = " "; break;
            case "overlong-region": request.Region = new string('x', 61); break;
            case "empty-child-name": request.Children[0].NameLocked = ""; break;
            case "overlong-child-name": request.Children[0].NameLocked = new string('x', 401); break;
            case "empty-birth-year": request.Children[0].BirthYearLocked = ""; break;
            case "missing-consent-version": request.Consent.NoticeVersion = ""; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    public void Dispose() => factory.Dispose();
}
