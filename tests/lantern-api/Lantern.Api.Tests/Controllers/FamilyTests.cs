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
    public async Task Register_Valid_Returns201AndMeReturnsTheSameFamily()
    {
        var uid = NewUid();
        using var client = Client(uid, "Meena Patel", "meena@example.test");

        var created = await client.RegisterAsync(ValidBody());

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var family = await created.Content.ReadFromJsonAsync<FamilyModel>();
        Assert.NotNull(family);
        Assert.NotEqual(Guid.Empty, family.FamilyId);
        Assert.NotEqual(Guid.Empty, family.Parent.ParentId);
        Assert.Equal("Meena Patel", family.Parent.Name);
        Assert.Equal("meena@example.test", family.Parent.Email);

        var me = await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me);
        Assert.NotNull(me);
        Assert.Equal(family.FamilyId, me.FamilyId);
        Assert.Equal("Gujarat", me.Region);
        Assert.Equal(family.Parent.ParentId, me.Parent.ParentId);
        Assert.Equal("gu", me.Parent.Language);
        Assert.Equal("2026-09", me.Parent.ConsentVersion);
        Assert.Equal(["Aarav", "Diya"], me.Children.Select(child => child.Name));
        Assert.Equal("Sunrise School", me.Children[0].School);
        Assert.Null(me.Children[1].School);
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
    [InlineData("born-too-long-ago")]
    [InlineData("born-too-recently")]
    [InlineData("language-not-offered")]
    [InlineData("empty-region")]
    [InlineData("overlong-region")]
    [InlineData("empty-child-name")]
    [InlineData("overlong-child-name")]
    [InlineData("control-character-in-name")]
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
    public async Task Register_StoredRows_HoldNoPlaintextAndNoUid()
    {
        var uid = NewUid();
        var name = $"Name-{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid():N}@example.test";
        var childName = $"Child-{Guid.NewGuid():N}";
        using var client = Client(uid, name, email);
        var body = ValidBody();
        body.Children[0].Name = childName;
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

        Assert.NotEmpty(stored);
        foreach (var secret in new[] { uid, name, email, childName, "Sunrise School" })
        {
            Assert.DoesNotContain(stored, value => value.Contains(secret, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Register_TwoFamiliesWithTheSameName_GetDifferentWrappedKeysAndCiphertext()
    {
        using var first = Client(NewUid(), "Meena Patel", "meena@example.test");
        using var second = Client(NewUid(), "Meena Patel", "meena@example.test");

        var firstFamily = await (await first.RegisterAsync(ValidBody())).Content.ReadFromJsonAsync<FamilyModel>();
        var secondFamily = await (await second.RegisterAsync(ValidBody())).Content.ReadFromJsonAsync<FamilyModel>();

        var families = new TableServiceClient(azurite.ConnectionString).GetTableClient(LanternApiFactory.FamiliesTable);
        var wrapped = new List<string>();
        foreach (var id in new[] { firstFamily!.FamilyId, secondFamily!.FamilyId })
        {
            var row = await families.GetEntityAsync<TableEntity>(id.ToString("D"), "family");
            wrapped.Add(row.Value.GetString("WrappedFieldKey"));
        }

        var parents = new TableServiceClient(azurite.ConnectionString).GetTableClient(LanternApiFactory.ParentsTable);
        var names = new List<string>();
        await foreach (var entity in parents.QueryAsync<TableEntity>(row => row.RowKey == "profile"))
        {
            if (new[] { firstFamily.FamilyId, secondFamily.FamilyId }.Any(id => id == entity.GetGuid("FamilyId")))
            {
                names.Add(entity.GetString("NameCipher"));
            }
        }

        Assert.NotEqual(wrapped[0], wrapped[1]);
        Assert.Equal(2, names.Count);
        Assert.NotEqual(names[0], names[1]);
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
            .Setup(r => r.RegisterAsync(It.IsAny<string>(), It.IsAny<Core.Models.Family>(), It.IsAny<Core.Models.Parent>(), It.IsAny<IReadOnlyList<Core.Models.Child>>(), It.IsAny<CancellationToken>()))
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
        var childName = $"Child-{Guid.NewGuid():N}";
        using var client = factory.CreateClient().WithBearer(token);
        var body = ValidBody();
        body.Children[0].Name = childName;

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

    private static FamilyRegisterRequest ValidBody() =>
        new()
        {
            Region = "Gujarat",
            Language = "gu",
            Consent = new ConsentModel { Accepted = true, NoticeVersion = "2026-09" },
            Children =
            [
                new ChildSaveModel { Name = "Aarav", ClassLevel = 1, BirthYear = DateTime.UtcNow.Year - 6, School = "Sunrise School" },
                new ChildSaveModel { Name = "Diya", ClassLevel = 3, BirthYear = DateTime.UtcNow.Year - 8 },
            ],
        };

    private static void Mutate(string scenario, FamilyRegisterRequest request)
    {
        var year = DateTime.UtcNow.Year;

        switch (scenario)
        {
            case "consent-declined": request.Consent.Accepted = false; break;
            case "no-children": request.Children = []; break;
            case "seven-children": request.Children = [.. Enumerable.Repeat(request.Children[0], 7)]; break;
            case "class-zero": request.Children[0].ClassLevel = 0; break;
            case "class-eleven": request.Children[0].ClassLevel = 11; break;
            case "born-too-long-ago": request.Children[0].BirthYear = year - 19; break;
            case "born-too-recently": request.Children[0].BirthYear = year - 2; break;
            case "language-not-offered": request.Language = "fr"; break;
            case "empty-region": request.Region = " "; break;
            case "overlong-region": request.Region = new string('x', 61); break;
            case "empty-child-name": request.Children[0].Name = " "; break;
            case "overlong-child-name": request.Children[0].Name = new string('x', 41); break;
            case "control-character-in-name": request.Children[0].Name = "Aar\u0007av"; break;
            case "missing-consent-version": request.Consent.NoticeVersion = ""; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    public void Dispose() => factory.Dispose();
}
