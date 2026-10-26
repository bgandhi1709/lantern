using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using Lantern.Api.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class AttemptsTests(AzuriteFixture azurite) : IDisposable
{
    private static readonly Chapter[] Mathematics =
    [
        new("gegp101", "gegp1", 1, "Large Numbers", "Reading and writing big numbers."),
        new("gegp102", "gegp1", 2, "Fractions", "Parts of a whole."),
        new("gegp201", "gegp2", 1, "Shapes", "Flat shapes and their sides."),
    ];

    private static readonly (Subject Subject, Chapter[] Chapters)[] ClassSeven = [(new Subject("mathematics", "Mathematics", "maths", Mathematics.Length), Mathematics)];

    private static readonly DateTimeOffset Day = new(2026, 10, 1, 16, 5, 40, TimeSpan.Zero);

    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    private BlobContainerClient Family => azurite.CreateBlobClient().GetBlobContainerClient(TestSettings.WorkspaceContainer);

    [Fact]
    public async Task Get_AChapterWithTwoAttempts_ReturnsItsHeader_AndEveryAttemptAndCheckNewestFirst()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client);
        await StartAsync(child, "gegp102", 3, Day);
        await DoneAsync(child, "gegp102", "c1", "c2", "c3");
        await CheckAsync(child, "gegp102", Day.AddDays(3), (true, "c1", "Equal parts"), (false, "c2", "Comparing fractions"));
        await CheckAsync(child, "gegp102", Day.AddDays(7), (true, "c1", "Equal parts"), (true, "c2", "Comparing fractions"));
        await StartAsync(child, "gegp102", 4, Day.AddDays(14));
        await DoneAsync(child, "gegp102", "c1");

        var response = await client.GetAsync(AttemptsOf(child, "gegp102"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(
            """
            {
              "chapter": { "chapterId": "gegp102", "number": 2, "title": "Fractions", "subjectId": "mathematics", "subjectName": "Mathematics",
                           "state": "planned", "attemptCount": 2, "checkCount": 2, "conceptsDone": 1, "conceptCount": 4 },
              "items": [
                { "attemptNumber": 2, "plannedAt": "2026-10-15T16:05:40+00:00", "state": "planned", "conceptsDone": 1, "conceptCount": 4, "checks": [] },
                { "attemptNumber": 1, "plannedAt": "2026-10-01T16:05:40+00:00", "state": "checked", "conceptsDone": 3, "conceptCount": 3,
                  "checks": [
                    { "checkId": "CHECK2", "checkNumber": 2, "takenAt": "2026-10-08T16:05:40+00:00", "right": 2, "of": 2, "wrong": [] },
                    { "checkId": "CHECK1", "checkNumber": 1, "takenAt": "2026-10-04T16:05:40+00:00", "right": 1, "of": 2,
                      "wrong": [ { "conceptId": "c2", "title": "Comparing fractions" } ] }
                  ] }
              ],
              "nextCursor": null
            }
            """
                .Replace("CHECK1", checkIds[0].ToString("D"), StringComparison.Ordinal)
                .Replace("CHECK2", checkIds[1].ToString("D"), StringComparison.Ordinal),
            await response.Content.ReadAsStringAsync()
        );
    }

    [Fact]
    public async Task Get_AChapterNeverStarted_IsAnEmptyHistory_WithANotStartedHeader()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client);

        var page = (await client.GetFromJsonAsync<JsonNode>(AttemptsOf(child, "gegp201")))!;

        Assert.Empty(page["items"]!.AsArray());
        Assert.Null(page["nextCursor"]);
        Assert.Equal(("not-started", 0, 0, 3), ((string)page["chapter"]!["state"]!, (int)page["chapter"]!["attemptCount"]!, (int)page["chapter"]!["checkCount"]!, (int)page["chapter"]!["number"]!));
        Assert.Null(page["chapter"]!["conceptCount"]);
    }

    [Fact]
    public async Task Get_AChapterTheChildsClassDoesNotHave_Is404ChapterNotFound()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client);

        var response = await client.GetAsync(AttemptsOf(child, "zzzz999"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("chapter-not-found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_ANewChildWhoseContentIsNotPreparedYet_Is409ContentNotReady()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client);

        var response = await client.GetAsync(AttemptsOf((family.FamilyId, family.Children.Single().ChildId), "gegp101"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("content-not-ready", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_AnotherFamilysChild_IsExactlyAsNotFoundAsAnIdThatDoesNotExist()
    {
        using var owner = Client(NewUid());
        using var stranger = Client(NewUid());
        var child = await ReadyChildAsync(owner);
        await StartAsync(child, "gegp101", 3, Day);
        await RegisterAsync(stranger);

        var theirs = await stranger.GetAsync(AttemptsOf(child, "gegp101"));
        var unknown = await stranger.GetAsync(AttemptsOf((child.FamilyId, Guid.NewGuid()), "gegp101"));

        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var theirText = await ProblemTextAsync(theirs);
        Assert.EndsWith("|child-not-found", theirText);
        Assert.Equal(await ProblemTextAsync(unknown), theirText);
    }

    [Fact]
    public async Task Get_AChildBeingDeleted_IsNotFound()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client);
        await StartAsync(child, "gegp101", 3, Day);
        Assert.Equal(HttpStatusCode.Accepted, (await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId))).StatusCode);

        var response = await client.GetAsync(AttemptsOf(child, "gegp101"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("child-not-found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_WithALimit_PagesTheAttempts_AndEachPageCarriesTheHeader()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client);
        await StartAsync(child, "gegp101", 2, Day);
        await StartAsync(child, "gegp101", 2, Day.AddDays(1));
        await StartAsync(child, "gegp101", 2, Day.AddDays(2));

        var first = (await client.GetFromJsonAsync<JsonNode>(AttemptsOf(child, "gegp101", "?limit=2")))!;
        var cursor = first["nextCursor"]!.GetValue<string>();
        var second = (await client.GetFromJsonAsync<JsonNode>(AttemptsOf(child, "gegp101", $"?limit=2&cursor={Uri.EscapeDataString(cursor)}")))!;

        Assert.Equal([3, 2], first["items"]!.AsArray().Select(item => (int)item!["attemptNumber"]!));
        Assert.Equal([1], second["items"]!.AsArray().Select(item => (int)item!["attemptNumber"]!));
        Assert.Null(second["nextCursor"]);
        Assert.Equal(3, (int)second["chapter"]!["attemptCount"]!);
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?cursor=not*base64")]
    public async Task Get_ALimitOrCursorThatIsNotValid_Is400InvalidPage(string query)
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client);

        var response = await client.GetAsync(AttemptsOf(child, "gegp101", query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-page", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_WithoutAToken_Is401_AndACallerWhoNeverRegistered_Is404NotRegistered()
    {
        using var anonymous = factory.CreateClient();
        using var unregistered = Client(NewUid());

        var denied = await anonymous.GetAsync(AttemptsOf((Guid.NewGuid(), Guid.NewGuid()), "gegp101"));
        var missing = await unregistered.GetAsync(AttemptsOf((Guid.NewGuid(), Guid.NewGuid()), "gegp101"));

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal("not-registered", await missing.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_PastThePerMinuteLimit_Is429_ForThatCallerOnly()
    {
        using var noisy = Client(NewUid());
        using var quiet = Client(NewUid());
        await RegisterAsync(quiet);

        HttpStatusCode last = default;
        for (var n = 0; n < 40; n++)
        {
            last = (await noisy.GetAsync(AttemptsOf((Guid.NewGuid(), Guid.NewGuid()), "gegp101"))).StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await quiet.GetAsync(AttemptsOf((Guid.NewGuid(), Guid.NewGuid()), "gegp101"))).StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private readonly List<Guid> checkIds = [];

    private async Task StartAsync((Guid FamilyId, Guid ChildId) child, string chapterId, int concepts, DateTimeOffset at)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IProgressRepository>()
            .StartAttemptAsync(new ChapterRef(child.FamilyId, child.ChildId, 7, chapterId), Guid.NewGuid(), concepts, "v1", at, CancellationToken.None);
    }

    private async Task DoneAsync((Guid FamilyId, Guid ChildId) child, string chapterId, params string[] conceptIds)
    {
        using var scope = factory.Services.CreateScope();
        var progress = scope.ServiceProvider.GetRequiredService<IProgressRepository>();
        foreach (var conceptId in conceptIds)
        {
            await progress.MarkConceptDoneAsync(new ChapterRef(child.FamilyId, child.ChildId, 7, chapterId), conceptId, Day, CancellationToken.None);
        }
    }

    private async Task CheckAsync((Guid FamilyId, Guid ChildId) child, string chapterId, DateTimeOffset at, params (bool Right, string Id, string Title)[] marks)
    {
        var checkId = Guid.NewGuid();
        checkIds.Add(checkId);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IProgressRepository>()
            .RecordCheckAsync(
                new ChapterRef(child.FamilyId, child.ChildId, 7, chapterId),
                checkId,
                at,
                [.. marks.Select(mark => new CheckedConcept { ConceptId = mark.Id, Title = mark.Title, Right = mark.Right })],
                "v1",
                CancellationToken.None
            );
    }

    private async Task<(Guid FamilyId, Guid ChildId)> ReadyChildAsync(HttpClient client)
    {
        var family = await RegisterAsync(client);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, 7, [.. ClassSeven.Select(entry => entry.Subject)]));
        await FamilyManifestSeed.PutSliceAsync(Family, family.FamilyId, child.ChildId, 7, ClassSeven);

        return (family.FamilyId, child.ChildId);
    }

    private static Uri AttemptsOf((Guid FamilyId, Guid ChildId) child, string chapterId, string query = "") =>
        new($"/v1/children/{child.ChildId}/chapters/{chapterId}/attempts{query}", UriKind.Relative);

    private static async Task<string> ProblemTextAsync(HttpResponseMessage response)
    {
        var problem = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        return $"{problem["status"]}|{problem["title"]}|{problem["detail"]}|{problem["code"]}";
    }

    private static void AssertJson(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"Expected {expected}{Environment.NewLine}Actual {actual}");

    private HttpClient Client(string uid) => factory.CreateClient().WithBearer(TestTokens.Create(uid));

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static string Locked(string plaintext) => $"v1.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext))}.{Guid.NewGuid():N}";

    private static async Task<FamilyModel> RegisterAsync(HttpClient client)
    {
        var body = new FamilyRegisterRequest
        {
            FamilyId = Guid.NewGuid(),
            Region = "Gujarat",
            Board = BoardType.Cbse,
            Language = "gu",
            ParentName = Locked("Meena Patel"),
            ParentEmail = Locked("meena@example.test"),
            RecoveryWrappedKey = Locked("recovery-key"),
            RecoverySalt = Locked("recovery-salt"),
            Consent = new ConsentModel { Accepted = true, NoticeVersion = "2026-09" },
            Children = [new ChildSaveModel { Name = Locked("Child"), ClassLevel = 7, BirthYear = Locked("2018"), School = Locked("Sunrise School") }],
        };

        var response = await client.RegisterAsync(body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<FamilyModel>())!;
    }
}
