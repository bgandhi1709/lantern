using System.Net;
using System.Net.Http.Json;
using Azure;
using Azure.Data.Tables;
using Lantern.Api.Contracts;
using Lantern.Api.Repository;
using Lantern.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class RegistrationTests(AzuriteFixture azurite) : IDisposable
{
    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    [Fact]
    public async Task Register_Valid_Returns201AndMeReturnsTheSameFamily()
    {
        var uid = NewUid();
        using var client = Client(uid, "Meena Patel", "meena@example.test");

        var created = await client.RegisterAsync(ValidBody());

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var family = await created.Content.ReadFromJsonAsync<FamilyResponse>();
        Assert.NotNull(family);
        Assert.NotEqual(Guid.Empty, family.FamilyId);
        Assert.Equal("Meena Patel", family.Name);
        Assert.Equal("meena@example.test", family.Email);

        var me = await client.GetFromJsonAsync<FamilyResponse>(ApiClientExtensions.Me);
        Assert.NotNull(me);
        Assert.Equal(family.FamilyId, me.FamilyId);
        Assert.Equal("Gujarat", me.Region);
        Assert.Equal("gu", me.Language);
        Assert.Equal("2026-09", me.ConsentVersion);
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

        var table = new TableServiceClient(azurite.ConnectionString).GetTableClient("parents");
        var stored = new List<string>();
        await foreach (var entity in table.QueryAsync<TableEntity>())
        {
            stored.Add(entity.PartitionKey);
            stored.AddRange(entity.Select(pair => pair.Value).OfType<string>());
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

        var firstFamily = await (await first.RegisterAsync(ValidBody())).Content.ReadFromJsonAsync<FamilyResponse>();
        var secondFamily = await (await second.RegisterAsync(ValidBody())).Content.ReadFromJsonAsync<FamilyResponse>();

        var table = new TableServiceClient(azurite.ConnectionString).GetTableClient("parents");
        var ids = new[] { firstFamily!.FamilyId.ToString("D"), secondFamily!.FamilyId.ToString("D") };
        var profiles = new List<TableEntity>();
        await foreach (var entity in table.QueryAsync<TableEntity>(row => row.RowKey == "profile"))
        {
            if (ids.Contains(entity.GetString("FamilyId")))
            {
                profiles.Add(entity);
            }
        }

        Assert.Equal(2, profiles.Count);
        Assert.NotEqual(profiles[0].GetString("WrappedFieldKey"), profiles[1].GetString("WrappedFieldKey"));
        Assert.NotEqual(profiles[0].GetString("NameCipher"), profiles[1].GetString("NameCipher"));
    }

    [Fact]
    public async Task Register_WhenStorageFails_IsAHardStopThatNeverLeaksTheDetail()
    {
        var repository = new Mock<IParentRepository>();
        repository
            .Setup(r => r.TryRegisterAsync(It.IsAny<Models.ParentProfile>(), It.IsAny<IReadOnlyList<Models.ChildRecord>>(), It.IsAny<CancellationToken>()))
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

    private HttpClient Client(string uid, string? name = null, string? email = null) =>
        factory.CreateClient().WithBearer(TestTokens.Create(uid, name, email));

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static RegisterBody ValidBody() =>
        new()
        {
            Region = "Gujarat",
            Language = "gu",
            Consent = new ConsentBody { Accepted = true, NoticeVersion = "2026-09" },
            Children =
            [
                new ChildBody { Name = "Aarav", ClassLevel = 1, BirthYear = DateTime.UtcNow.Year - 6, School = "Sunrise School" },
                new ChildBody { Name = "Diya", ClassLevel = 3, BirthYear = DateTime.UtcNow.Year - 8 },
            ],
        };

    private static void Mutate(string scenario, RegisterBody body)
    {
        var year = DateTime.UtcNow.Year;

        switch (scenario)
        {
            case "consent-declined": body.Consent.Accepted = false; break;
            case "no-children": body.Children = []; break;
            case "seven-children": body.Children = [.. Enumerable.Repeat(body.Children[0], 7)]; break;
            case "class-zero": body.Children[0].ClassLevel = 0; break;
            case "class-eleven": body.Children[0].ClassLevel = 11; break;
            case "born-too-long-ago": body.Children[0].BirthYear = year - 19; break;
            case "born-too-recently": body.Children[0].BirthYear = year - 2; break;
            case "language-not-offered": body.Language = "fr"; break;
            case "empty-region": body.Region = " "; break;
            case "overlong-region": body.Region = new string('x', 61); break;
            case "empty-child-name": body.Children[0].Name = " "; break;
            case "overlong-child-name": body.Children[0].Name = new string('x', 41); break;
            case "control-character-in-name": body.Children[0].Name = "Aar\u0007av"; break;
            case "missing-consent-version": body.Consent.NoticeVersion = ""; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    public void Dispose() => factory.Dispose();
}
