using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using Lantern.Api.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Models;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class ChaptersTests(AzuriteFixture azurite) : IDisposable
{
    private static readonly Chapter[] English =
    [
        new("gepr101", "gepr1", 1, "A Bottle of Dew", "A story that starts from a small, ordinary thing."),
        new("gepr102", "gepr1", 2, "A Tale of Two Birds", "Two birds, two ways of living."),
    ];

    private static readonly Chapter[] Mathematics =
    [
        new("gegp101", "gegp1", 1, "Large Numbers", "Reading and writing big numbers."),
        new("gegp102", "gegp1", 2, "Fractions", "Parts of a whole."),
        new("gegp201", "gegp2", 1, "Shapes", "Flat shapes and their sides."),
    ];

    private static readonly (Subject Subject, Chapter[] Chapters)[] ClassSeven =
    [
        (new Subject("english", "English", "english", English.Length), English),
        (new Subject("mathematics", "Mathematics", "maths", Mathematics.Length), Mathematics),
    ];

    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    private BlobContainerClient Family => azurite.CreateBlobClient().GetBlobContainerClient(TestSettings.WorkspaceContainer);

    [Fact]
    public async Task Get_ASubjectOfAReadyChild_ReturnsItsChaptersInBookOrderAsAPage()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client, BoardType.Cbse, 7);

        var response = await client.GetAsync(ChaptersOf(child, "english"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(
            """
            {
              "items": [
                { "chapterId": "gepr101", "number": 1, "title": "A Bottle of Dew",      "summary": "A story that starts from a small, ordinary thing." },
                { "chapterId": "gepr102", "number": 2, "title": "A Tale of Two Birds", "summary": "Two birds, two ways of living." }
              ],
              "nextCursor": null
            }
            """,
            await response.Content.ReadAsStringAsync()
        );
    }

    [Fact]
    public async Task Get_ASubjectMadeOfSeveralBooks_ListsAllTheirChapters_NumberedOneToNAcrossTheSubject()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client, BoardType.Cbse, 7);

        var page = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child, "mathematics")))!;

        Assert.Equal(["gegp101", "gegp102", "gegp201"], page["items"]!.AsArray().Select(item => (string)item!["chapterId"]!));
        Assert.Equal([1, 2, 3], page["items"]!.AsArray().Select(item => (int)item!["number"]!));
    }

    [Fact]
    public async Task Get_ForAnSscChild_ReturnsTheirChapters()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client, BoardType.Ssc, 2);

        var page = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child, "english")))!;

        Assert.Equal(["gepr101", "gepr102"], page["items"]!.AsArray().Select(item => (string)item!["chapterId"]!));
    }

    [Fact]
    public async Task Get_ASubjectTheChildsClassDoesNotHave_Is404SubjectNotFound()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client, BoardType.Cbse, 7);

        var response = await client.GetAsync(ChaptersOf(child, "astronomy"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("subject-not-found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_ANewChildWhoseContentIsNotPreparedYet_Is409ContentNotReady()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 7);

        var response = await client.GetAsync(ChaptersOf((family.FamilyId, family.Children.Single().ChildId), "english"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("content-not-ready", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_AChildWhoseSliceManifestIsMissing_Is409ContentNotReady_EvenWithAFamilyManifestEntry()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 7);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, 7, [.. ClassSeven.Select(entry => entry.Subject)]));

        var response = await client.GetAsync(ChaptersOf((family.FamilyId, child.ChildId), "english"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("content-not-ready", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_ASubjectInTheFamilyManifestButNotInTheSliceManifest_Is409ContentNotReady()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 7);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, 7, [.. ClassSeven.Select(entry => entry.Subject)]));
        await FamilyManifestSeed.PutSliceAsync(Family, family.FamilyId, child.ChildId, 7, ClassSeven[0]);

        var response = await client.GetAsync(ChaptersOf((family.FamilyId, child.ChildId), "mathematics"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("content-not-ready", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_AnotherFamilysChild_IsExactlyAsNotFoundAsAnIdThatDoesNotExist()
    {
        using var owner = Client(NewUid());
        using var stranger = Client(NewUid());
        var child = await ReadyChildAsync(owner, BoardType.Cbse, 7);
        await RegisterAsync(stranger, BoardType.Cbse, 7);

        var theirs = await stranger.GetAsync(ChaptersOf(child, "english"));
        var unknown = await stranger.GetAsync(ChaptersOf((child.FamilyId, Guid.NewGuid()), "english"));

        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var theirText = await ProblemTextAsync(theirs);
        Assert.EndsWith("|child-not-found", theirText);
        Assert.Equal(await ProblemTextAsync(unknown), theirText);
    }

    [Fact]
    public async Task Get_AChildBeingDeleted_IsNotFound_EvenThoughTheirManifestsAreStillThere()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client, BoardType.Cbse, 7);
        Assert.Equal(HttpStatusCode.Accepted, (await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId))).StatusCode);

        var response = await client.GetAsync(ChaptersOf(child, "english"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("child-not-found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_WithALimit_PagesTheChapters_AndTheCursorLeadsToTheRest_KeepingTheRunningNumbers()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client, BoardType.Cbse, 7);

        var first = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child, "mathematics", "?limit=2")))!;
        var cursor = first["nextCursor"]!.GetValue<string>();
        var second = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child, "mathematics", $"?limit=2&cursor={Uri.EscapeDataString(cursor)}")))!;

        Assert.Equal(["gegp101", "gegp102"], first["items"]!.AsArray().Select(item => (string)item!["chapterId"]!));
        Assert.False(string.IsNullOrWhiteSpace(cursor));
        Assert.Equal(["gegp201"], second["items"]!.AsArray().Select(item => (string)item!["chapterId"]!));
        Assert.Equal([3], second["items"]!.AsArray().Select(item => (int)item!["number"]!));
        Assert.Null(second["nextCursor"]);
    }

    public static TheoryData<string> InvalidQueries =>
        new() { "?limit=0", "?limit=101", "?limit=-5", "?cursor=not*base64", CursorAt("abc"), CursorAt("0"), CursorAt("3") };

    private static string CursorAt(string text) => "?cursor=" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)).TrimEnd('=');

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Get_ALimitOrCursorThatIsNotValid_Is400InvalidPage(string query)
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client, BoardType.Cbse, 7);

        var response = await client.GetAsync(ChaptersOf(child, "mathematics", query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-page", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_ASliceManifestSchemaThisVersionDoesNotKnow_IsAServerError_NeverAnEmptyOrWrongList()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 7);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, 7, [.. ClassSeven.Select(entry => entry.Subject)]));
        await FamilyManifestSeed.PutSliceRawAsync(Family, family.FamilyId, child.ChildId, 7, FamilyManifestSeed.ClassManifestJson("class-manifest-v9", 7, ClassSeven));

        var response = await client.GetAsync(ChaptersOf((family.FamilyId, child.ChildId), "english"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Bottle of Dew", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_WithoutAToken_Is401_AndACallerWhoNeverRegistered_Is404NotRegistered()
    {
        using var anonymous = factory.CreateClient();
        using var unregistered = Client(NewUid());

        var denied = await anonymous.GetAsync(ChaptersOf((Guid.NewGuid(), Guid.NewGuid()), "english"));
        var missing = await unregistered.GetAsync(ChaptersOf((Guid.NewGuid(), Guid.NewGuid()), "english"));

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not-registered", await missing.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_ASubjectIdLongerThanAnyRealOne_IsNotARoute()
    {
        using var client = Client(NewUid());
        var child = await ReadyChildAsync(client, BoardType.Cbse, 7);

        var response = await client.GetAsync(ChaptersOf(child, new string('a', 65)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_PastThePerMinuteLimit_Is429_ForThatCallerOnly()
    {
        using var noisy = Client(NewUid());
        using var quiet = Client(NewUid());
        await RegisterAsync(quiet, BoardType.Cbse, 7);

        HttpStatusCode last = default;
        for (var n = 0; n < 40; n++)
        {
            last = (await noisy.GetAsync(ChaptersOf((Guid.NewGuid(), Guid.NewGuid()), "english"))).StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await quiet.GetAsync(ChaptersOf((Guid.NewGuid(), Guid.NewGuid()), "english"))).StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private static Uri ChaptersOf((Guid FamilyId, Guid ChildId) child, string subjectId, string query = "") =>
        new($"/v1/children/{child.ChildId}/subjects/{subjectId}/chapters{query}", UriKind.Relative);

    private static async Task<string> ProblemTextAsync(HttpResponseMessage response)
    {
        var problem = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        return $"{problem["status"]}|{problem["title"]}|{problem["detail"]}|{problem["code"]}";
    }

    private static void AssertJson(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"Expected {expected}{Environment.NewLine}Actual {actual}");

    private HttpClient Client(string uid) => factory.CreateClient().WithBearer(TestTokens.Create(uid));

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private async Task<(Guid FamilyId, Guid ChildId)> ReadyChildAsync(HttpClient client, BoardType board, int classLevel)
    {
        var family = await RegisterAsync(client, board, classLevel);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, classLevel, [.. ClassSeven.Select(entry => entry.Subject)]));
        await FamilyManifestSeed.PutSliceAsync(Family, family.FamilyId, child.ChildId, classLevel, ClassSeven);

        return (family.FamilyId, child.ChildId);
    }

    private static string Locked(string plaintext) => $"v1.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext))}.{Guid.NewGuid():N}";

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
}
