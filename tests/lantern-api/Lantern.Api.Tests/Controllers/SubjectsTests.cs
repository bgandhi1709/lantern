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
public sealed class SubjectsTests(AzuriteFixture azurite) : IDisposable
{
    private static readonly Subject[] ClassThree =
    [
        new("english", "English", "english", 12),
        new("mathematics", "Mathematics", "maths", 14),
        new("the-world-around-us", "The World Around Us", "evs", 12),
    ];

    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    private BlobContainerClient Family => azurite.CreateBlobClient().GetBlobContainerClient(TestSettings.WorkspaceContainer);

    [Fact]
    public async Task Get_AChildWhoseContentIsReady_ReturnsTheirSubjectsAsAPage()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, 3, ClassThree));

        var response = await client.GetAsync(SubjectsOf(child.ChildId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(
            """
            {
              "items": [
                { "subjectId": "english",             "name": "English",             "icon": "english", "chapterCount": 12 },
                { "subjectId": "mathematics",         "name": "Mathematics",         "icon": "maths",   "chapterCount": 14 },
                { "subjectId": "the-world-around-us", "name": "The World Around Us", "icon": "evs",     "chapterCount": 12 }
              ],
              "nextCursor": null
            }
            """,
            await response.Content.ReadAsStringAsync()
        );
    }

    [Fact]
    public async Task Get_ANewChildWhoseContentIsNotPreparedYet_Is409ContentNotReady()
    {
        using var client = Client(NewUid());
        var child = (await RegisterAsync(client, BoardType.Cbse, 3)).Children.Single();

        var response = await client.GetAsync(SubjectsOf(child.ChildId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("content-not-ready", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_AChildWithNoEntryWhileASiblingIsReady_Is409_AndTheSiblingStillGetsTheirSubjects()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3, 6);
        var (ready, waiting) = (family.Children[0], family.Children[1]);
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (ready.ChildId, 3, ClassThree));

        var notReady = await client.GetAsync(SubjectsOf(waiting.ChildId));
        var readyResponse = await client.GetAsync(SubjectsOf(ready.ChildId));

        Assert.Equal(HttpStatusCode.Conflict, notReady.StatusCode);
        Assert.Equal("content-not-ready", await notReady.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
    }

    [Fact]
    public async Task Get_AnotherFamilysChild_IsExactlyAsNotFoundAsAnIdThatDoesNotExist()
    {
        using var owner = Client(NewUid());
        using var stranger = Client(NewUid());
        var child = (await RegisterAsync(owner, BoardType.Cbse, 3)).Children.Single();
        await RegisterAsync(stranger, BoardType.Cbse, 3);
        await FamilyManifestSeed.PutAsync(Family, (await owner.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me))!.FamilyId, (child.ChildId, 3, ClassThree));

        var theirs = await stranger.GetAsync(SubjectsOf(child.ChildId));
        var unknown = await stranger.GetAsync(SubjectsOf(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var theirText = await ProblemTextAsync(theirs);
        Assert.EndsWith("|child-not-found", theirText);
        Assert.Equal(await ProblemTextAsync(unknown), theirText);
    }

    [Fact]
    public async Task Get_AChildBeingDeleted_IsNotFound_EvenThoughTheirEntryIsStillInTheManifest()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, 3, ClassThree));
        Assert.Equal(HttpStatusCode.Accepted, (await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId))).StatusCode);

        var response = await client.GetAsync(SubjectsOf(child.ChildId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("child-not-found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_WithALimit_PagesTheList_AndTheCursorLeadsToTheRest()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, 3, ClassThree));

        var first = (await client.GetFromJsonAsync<JsonNode>(SubjectsOf(child.ChildId, "?limit=2")))!;
        var cursor = first["nextCursor"]!.GetValue<string>();
        var second = (await client.GetFromJsonAsync<JsonNode>(SubjectsOf(child.ChildId, $"?limit=2&cursor={Uri.EscapeDataString(cursor)}")))!;

        Assert.Equal(["english", "mathematics"], first["items"]!.AsArray().Select(item => item!["subjectId"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(cursor));
        Assert.Equal(["the-world-around-us"], second["items"]!.AsArray().Select(item => item!["subjectId"]!.GetValue<string>()));
        Assert.Null(second["nextCursor"]);
    }

    [Fact]
    public async Task Get_ALimitThatCoversTheWholeList_HasNoNextCursor()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, 3, ClassThree));

        var page = (await client.GetFromJsonAsync<JsonNode>(SubjectsOf(child.ChildId, "?limit=3")))!;

        Assert.Equal(3, page["items"]!.AsArray().Count);
        Assert.Null(page["nextCursor"]);
    }

    public static TheoryData<string> InvalidQueries =>
        new()
        {
            "?limit=0",
            "?limit=101",
            "?limit=-5",
            "?cursor=not*base64",
            CursorAt("abc"),
            CursorAt("-1"),
            CursorAt("0"),
            CursorAt("3"),
            "?limit=2&" + CursorAt("3").TrimStart('?'),
        };

    private static string CursorAt(string text) => "?cursor=" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)).TrimEnd('=');

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Get_ALimitOrCursorThatIsNotValid_Is400InvalidPage(string query)
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, 3, ClassThree));

        var response = await client.GetAsync(SubjectsOf(child.ChildId, query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-page", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_ALimitThatIsNotANumber_Is400()
    {
        using var client = Client(NewUid());
        var child = (await RegisterAsync(client, BoardType.Cbse, 3)).Children.Single();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(SubjectsOf(child.ChildId, "?limit=abc"))).StatusCode);
    }

    [Fact]
    public async Task Get_AnEmptyCursor_IsTheFirstPage()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutAsync(Family, family.FamilyId, (child.ChildId, 3, ClassThree));

        var page = (await client.GetFromJsonAsync<JsonNode>(SubjectsOf(child.ChildId, "?cursor=")))!;

        Assert.Equal(3, page["items"]!.AsArray().Count);
    }

    [Fact]
    public async Task Get_ASchemaThisVersionDoesNotKnow_IsAServerError_NeverAnEmptyOrWrongList()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, BoardType.Cbse, 3);
        var child = family.Children.Single();
        await FamilyManifestSeed.PutRawAsync(Family, family.FamilyId, FamilyManifestSeed.Json("family-manifest-v9", (child.ChildId, 3, ClassThree)));

        var response = await client.GetAsync(SubjectsOf(child.ChildId));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("english", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_WithoutAToken_Is401_AndACallerWhoNeverRegistered_Is404NotRegistered()
    {
        using var anonymous = factory.CreateClient();
        using var unregistered = Client(NewUid());

        var denied = await anonymous.GetAsync(SubjectsOf(Guid.NewGuid()));
        var missing = await unregistered.GetAsync(SubjectsOf(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not-registered", await missing.ProblemCodeAsync());
    }

    [Fact]
    public async Task Get_PastThePerMinuteLimit_Is429_ForThatCallerOnly()
    {
        using var noisy = Client(NewUid());
        using var quiet = Client(NewUid());
        await RegisterAsync(quiet, BoardType.Cbse, 3);

        HttpStatusCode last = default;
        for (var n = 0; n < 40; n++)
        {
            last = (await noisy.GetAsync(SubjectsOf(Guid.NewGuid()))).StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await quiet.GetAsync(SubjectsOf(Guid.NewGuid()))).StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private static async Task<string> ProblemTextAsync(HttpResponseMessage response)
    {
        var problem = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        return $"{problem["status"]}|{problem["title"]}|{problem["detail"]}|{problem["code"]}";
    }

    private static Uri SubjectsOf(Guid childId, string query = "") => new($"/v1/children/{childId}/subjects{query}", UriKind.Relative);

    private static void AssertJson(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"Expected {expected}{Environment.NewLine}Actual {actual}");

    private HttpClient Client(string uid) => factory.CreateClient().WithBearer(TestTokens.Create(uid));

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

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
