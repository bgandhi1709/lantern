using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Lantern.Api.Models;
using Lantern.Core.Constants;

namespace Lantern.Api.Test.Integration.E2E;

[Trait("Category", "E2E")]
public sealed class ChaptersE2ETests
{
    private static readonly Uri Register = new("/v1/register", UriKind.Relative);

    [Theory]
    [InlineData(BoardType.Cbse, 3)]
    [InlineData(BoardType.Cbse, 10)]
    [InlineData(BoardType.Ssc, 2)]
    public async Task EachSubjectOfAChild_ListsItsBuiltChapters_NumberedOneToNInBookOrder(BoardType board, int classLevel)
    {
        using var client = await NewParentAsync();
        var child = (await RegisterAsync(client, board, classLevel)).Children.Single();
        var subjects = (JsonNode.Parse(await WaitForSubjectsAsync(client, child.ChildId))!)["items"]!.AsArray();

        Assert.NotEmpty(subjects);
        foreach (var subject in subjects)
        {
            var page = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child.ChildId, (string)subject!["subjectId"]!)))!;
            var chapters = page["items"]!.AsArray();

            Assert.Equal((int)subject["chapterCount"]!, chapters.Count);
            Assert.Equal(Enumerable.Range(1, chapters.Count), chapters.Select(chapter => (int)chapter!["number"]!));
            Assert.All(chapters, chapter =>
            {
                Assert.False(string.IsNullOrWhiteSpace((string)chapter!["title"]!));
                Assert.False(string.IsNullOrWhiteSpace((string)chapter["summary"]!));
            });
        }
    }

    [Fact]
    public async Task ASubjectsChapters_CanBePaged_AndAnUnknownSubjectIsNotFound()
    {
        using var client = await NewParentAsync();
        var child = (await RegisterAsync(client, BoardType.Cbse, 3)).Children.Single();
        await WaitForSubjectsAsync(client, child.ChildId);

        var first = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child.ChildId, "mathematics", "?limit=1")))!;
        var cursor = first["nextCursor"]!.GetValue<string>();
        var second = (await client.GetFromJsonAsync<JsonNode>(ChaptersOf(child.ChildId, "mathematics", $"?limit=1&cursor={Uri.EscapeDataString(cursor)}")))!;

        Assert.Equal([1], first["items"]!.AsArray().Select(chapter => (int)chapter!["number"]!));
        Assert.Equal([2], second["items"]!.AsArray().Select(chapter => (int)chapter!["number"]!));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ChaptersOf(child.ChildId, "astronomy"))).StatusCode);
    }

    [Fact]
    public async Task AnotherFamilysChild_AndACallWithNoToken_AreRefused()
    {
        using var owner = await NewParentAsync();
        using var stranger = await NewParentAsync();
        using var anonymous = new HttpClient { BaseAddress = new Uri(RequireEnv("E2E_BASE_URL")) };
        var child = (await RegisterAsync(owner, BoardType.Cbse, 3)).Children.Single();
        await RegisterAsync(stranger, BoardType.Cbse, 3);
        await WaitForSubjectsAsync(owner, child.ChildId);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(ChaptersOf(child.ChildId, "mathematics"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(ChaptersOf(child.ChildId, "mathematics"))).StatusCode);
    }

    private static async Task<string> WaitForSubjectsAsync(HttpClient client, Guid childId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync(SubjectsOf(childId));
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return await response.Content.ReadAsStringAsync();
            }

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("content-not-ready", (await response.Content.ReadFromJsonAsync<JsonNode>())!["code"]!.GetValue<string>());
            await Task.Delay(500);
        }

        throw new TimeoutException("The Functions app did not make the Child's Subjects available in time.");
    }

    private static Uri SubjectsOf(Guid childId) => new($"/v1/children/{childId}/subjects", UriKind.Relative);

    private static Uri ChaptersOf(Guid childId, string subjectId, string query = "") => new($"/v1/children/{childId}/subjects/{subjectId}/chapters{query}", UriKind.Relative);

    private static async Task<FamilyModel> RegisterAsync(HttpClient client, BoardType board, params int[] classes)
    {
        var body = RegisteredFamilyFixture.ValidBody();
        body.Board = board;
        body.Children = [.. classes.Select(classLevel => new ChildSaveModel { Name = "e2e-locked-child-name", ClassLevel = classLevel, BirthYear = "e2e-locked-birth-year", School = "e2e-locked-school" })];

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
