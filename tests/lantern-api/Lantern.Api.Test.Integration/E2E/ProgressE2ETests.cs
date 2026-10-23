using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Lantern.Api.Models;
using Lantern.Core.Constants;

namespace Lantern.Api.Test.Integration.E2E;

[Trait("Category", "E2E")]
public sealed class ProgressE2ETests
{
    private static readonly Uri Register = new("/v1/register", UriKind.Relative);

    private static readonly string[] SeededStates = ["planned", "checked", "taught", "planned"];

    [Fact]
    public async Task AFreshChild_HasEveryChapterNotStarted_AndNoHistory()
    {
        using var client = await NewParentAsync();
        var child = (await RegisterAsync(client)).Children.Single();
        var (subjectId, _) = await FirstSubjectAsync(client, child.ChildId);

        var page = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child.ChildId, subjectId)))!;
        var firstChapter = (string)page["items"]![0]!["chapterId"]!;
        var history = (await client.GetFromJsonAsync<JsonNode>(AttemptsOf(child.ChildId, firstChapter)))!;

        Assert.All(page["items"]!.AsArray(), item => Assert.Equal("not-started", (string)item!["state"]!));
        Assert.Equal(0, (int)page["progress"]!["checked"]! + (int)page["progress"]!["taught"]! + (int)page["progress"]!["planned"]!);
        Assert.Empty(history["items"]!.AsArray());
        Assert.Equal("not-started", (string)history["chapter"]!["state"]!);
    }

    [Fact]
    public async Task AChildWithHistory_ShowsEachChaptersState_AndEveryAttemptAndCheck()
    {
        using var client = await NewParentAsync();
        var child = (await RegisterAsync(client)).Children.Single();
        var (subjectId, _) = await FirstSubjectAsync(client, child.ChildId);
        await SeedAsync(child.ChildId);

        var page = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child.ChildId, subjectId)))!;
        var items = page["items"]!.AsArray();
        var expected = Enumerable.Range(0, items.Count).Select(index => index < SeededStates.Length ? SeededStates[index] : "not-started");
        var history = (await client.GetFromJsonAsync<JsonNode>(AttemptsOf(child.ChildId, (string)items[0]!["chapterId"]!)))!;
        var attempts = history["items"]!.AsArray();

        Assert.Equal(expected, items.Select(item => (string)item!["state"]!));
        Assert.Equal(items.Count, page["progress"]!["states"]!.AsArray().Count);
        Assert.Equal((2, 2, "planned", 2), ((int)history["chapter"]!["attemptCount"]!, (int)history["chapter"]!["checkCount"]!, (string)history["chapter"]!["state"]!, (int)history["chapter"]!["conceptsDone"]!));
        Assert.True((int)history["chapter"]!["conceptCount"]! >= 3);
        Assert.Equal([2, 1], attempts.Select(attempt => (int)attempt!["attemptNumber"]!));
        Assert.Equal(["planned", "checked"], attempts.Select(attempt => (string)attempt!["state"]!));
        Assert.Empty(attempts[0]!["checks"]!.AsArray());
        var checks = attempts[1]!["checks"]!.AsArray();
        Assert.Equal([(2, 1), (1, 2)], checks.Select(check => ((int)check!["checkNumber"]!, check["wrong"]!.AsArray().Count)));
        Assert.All(checks, check => Assert.Equal((int)check!["of"]! - check["wrong"]!.AsArray().Count, (int)check["right"]!));
        Assert.All(checks.SelectMany(check => check!["wrong"]!.AsArray()), wrong => Assert.False(string.IsNullOrWhiteSpace((string)wrong!["title"]!)));
    }

    [Fact]
    public async Task TheListPagesAndSearchesOnTheSeededMasters()
    {
        using var client = await NewParentAsync();
        var child = (await RegisterAsync(client)).Children.Single();
        var (subjectId, count) = await FirstSubjectAsync(client, child.ChildId);
        await SeedAsync(child.ChildId);

        var limit = Math.Max(1, (count + 2) / 3);
        var numbers = new List<int>();
        string cursor = null;
        do
        {
            var query = cursor is null ? $"?limit={limit}" : $"?limit={limit}&cursor={Uri.EscapeDataString(cursor)}";
            var page = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child.ChildId, subjectId, query)))!;
            numbers.AddRange(page["items"]!.AsArray().Select(item => (int)item!["number"]!));
            cursor = (string)page["nextCursor"];
        }
        while (cursor is not null);

        var everything = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child.ChildId, subjectId)))!["items"]!.AsArray();
        var title = (string)everything[0]!["title"]!;
        var found = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child.ChildId, subjectId, $"?q={Uri.EscapeDataString(title.ToUpperInvariant())}")))!["items"]!.AsArray();

        Assert.Equal(Enumerable.Range(1, everything.Count), numbers);
        Assert.Contains(found, item => (int)item!["number"]! == 1);
    }

    [Fact]
    public async Task AnotherFamilysChild_AndACallWithNoToken_CannotReadTheHistory()
    {
        using var owner = await NewParentAsync();
        using var stranger = await NewParentAsync();
        using var anonymous = new HttpClient { BaseAddress = new Uri(RequireEnv("E2E_BASE_URL")) };
        var child = (await RegisterAsync(owner)).Children.Single();
        await RegisterAsync(stranger);
        var (subjectId, _) = await FirstSubjectAsync(owner, child.ChildId);
        await SeedAsync(child.ChildId);
        var chapterId = (string)(await owner.GetFromJsonAsync<JsonNode>(ChaptersOf(child.ChildId, subjectId)))!["items"]![0]!["chapterId"]!;

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(AttemptsOf(child.ChildId, chapterId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(ChaptersOf(child.ChildId, subjectId))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(AttemptsOf(child.ChildId, chapterId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(AttemptsOf(child.ChildId, "no-such-chapter"))).StatusCode);
    }

    private static async Task SeedAsync(Guid childId)
    {
        using var local = new HttpClient { BaseAddress = new Uri(RequireEnv("E2E_BASE_URL")) };
        var response = await local.PostAsync(new Uri($"/dev/seed-progress?childId={childId}", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (int)(await response.Content.ReadFromJsonAsync<JsonNode>())!["seeded"]!);
    }

    private static async Task<(string SubjectId, int Count)> FirstSubjectAsync(HttpClient client, Guid childId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync(SubjectsOf(childId));
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var first = (await response.Content.ReadFromJsonAsync<JsonNode>())!["items"]![0]!;

                return ((string)first["subjectId"]!, (int)first["chapterCount"]!);
            }

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(500);
        }

        throw new TimeoutException("The Functions app did not make the Child's Subjects available in time.");
    }

    private static Uri SubjectsOf(Guid childId) => new($"/v1/children/{childId}/subjects", UriKind.Relative);

    private static Uri ChaptersOf(Guid childId, string subjectId, string query = "") => new($"/v1/children/{childId}/subjects/{subjectId}/chapters{query}", UriKind.Relative);

    private static Uri AttemptsOf(Guid childId, string chapterId) => new($"/v1/children/{childId}/chapters/{chapterId}/attempts", UriKind.Relative);

    private static async Task<FamilyModel> RegisterAsync(HttpClient client)
    {
        var body = RegisteredFamilyFixture.ValidBody();
        body.Board = BoardType.Cbse;
        body.Children = [new ChildSaveModel { Name = "e2e-locked-child-name", ClassLevel = 3, BirthYear = "e2e-locked-birth-year", School = "e2e-locked-school" }];

        var response = await client.PostAsJsonAsync(Register, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<FamilyModel>())!;
    }

    private static async Task<HttpClient> NewParentAsync()
    {
        var token = await EmulatorTokens.MintAsync(RequireEnv("E2E_AUTH_EMULATOR"), $"e2e-{Guid.NewGuid():N}");
        var client = new HttpClient { BaseAddress = new Uri(RequireEnv("E2E_BASE_URL")) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static string RequireEnv(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is required to run the E2E tests.");
}
