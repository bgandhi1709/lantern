using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Lantern.Api.Models;
using Lantern.Core.Constants;

namespace Lantern.Api.Test.Integration.E2E;

[Trait("Category", "E2E")]
public sealed class SubjectsE2ETests
{
    private static readonly Uri Register = new("/v1/register", UriKind.Relative);

    [Fact]
    public async Task ACbseFamily_GetsEachChildsSubjects_OnceTheBackgroundWorkIsDone()
    {
        using var client = await NewParentAsync();
        var family = await RegisterAsync(client, BoardType.Cbse, 3, 6);

        foreach (var child in family.Children)
        {
            var page = await WaitForSubjectsAsync(client, child.ChildId);

            AssertJson(
                """
                {
                  "items": [ { "subjectId": "mathematics", "name": "Mathematics", "icon": "maths", "chapterCount": 2 } ],
                  "nextCursor": null
                }
                """,
                page
            );
        }
    }

    [Fact]
    public async Task AnSscFamily_GetsItsSubjects_AndCanPageThem()
    {
        using var client = await NewParentAsync();
        var child = (await RegisterAsync(client, BoardType.Ssc, 2)).Children.Single();
        await WaitForSubjectsAsync(client, child.ChildId);

        var first = (await client.GetFromJsonAsync<JsonNode>(SubjectsOf(child.ChildId, "?limit=1")))!;
        var cursor = first["nextCursor"]!.GetValue<string>();
        var second = (await client.GetFromJsonAsync<JsonNode>(SubjectsOf(child.ChildId, $"?limit=1&cursor={Uri.EscapeDataString(cursor)}")))!;

        Assert.Equal(["mathematics"], first["items"]!.AsArray().Select(item => (string)item!["subjectId"]!));
        Assert.Equal(["english"], second["items"]!.AsArray().Select(item => (string)item!["subjectId"]!));
        Assert.Null(second["nextCursor"]);
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

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(SubjectsOf(child.ChildId))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(SubjectsOf(child.ChildId))).StatusCode);
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

    private static Uri SubjectsOf(Guid childId, string query = "") => new($"/v1/children/{childId}/subjects{query}", UriKind.Relative);

    private static void AssertJson(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"Expected {expected}{Environment.NewLine}Actual {actual}");

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
