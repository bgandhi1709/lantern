using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Microsoft.Azure.Cosmos;

namespace Lantern.Api.Tests.Infrastructure;

internal static class ContentSeed
{
    public static Task PutClassAsync(Container content, BoardType board, int classLevel, params (Subject Subject, Chapter[] Chapters)[] subjects) =>
        PutClassRawAsync(content, board, classLevel, ManifestJson("class-manifest-v1", BoardKey(board), classLevel, subjects));

    public static async Task PutClassRawAsync(Container content, BoardType board, int classLevel, string manifestJson)
    {
        var classKey = ClassKey(board, classLevel);
        var document = new JsonObject
        {
            ["id"] = $"class:{classKey}",
            ["kind"] = "class",
            ["classKey"] = classKey,
            ["manifest"] = JsonNode.Parse(manifestJson),
            ["release"] = "test-release",
        };

        using var body = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        using var response = await content.UpsertItemStreamAsync(body, new PartitionKey(classKey));
        response.EnsureSuccessStatusCode();
    }

    public static async Task RemoveClassAsync(Container content, BoardType board, int classLevel)
    {
        var classKey = ClassKey(board, classLevel);
        using var response = await content.DeleteItemStreamAsync($"class:{classKey}", new PartitionKey(classKey));

        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public static string ManifestJson(string schema, string board, int classLevel, params (Subject Subject, Chapter[] Chapters)[] subjects) =>
        new JsonObject
        {
            ["schema"] = schema,
            ["board"] = board,
            ["profileVersion"] = $"{board}-v1",
            ["class"] = classLevel,
            ["subjects"] = new JsonArray(
                [
                    .. subjects.Select(entry => (JsonNode)new JsonObject
                    {
                        ["subjectId"] = entry.Subject.SubjectId,
                        ["name"] = entry.Subject.Name,
                        ["icon"] = entry.Subject.Icon,
                        ["chapterCount"] = entry.Chapters.Length,
                        ["chapters"] = new JsonArray(
                            [
                                .. entry.Chapters.Select(chapter => (JsonNode)new JsonObject
                                {
                                    ["chapterId"] = chapter.ChapterId,
                                    ["bookId"] = chapter.BookId,
                                    ["number"] = chapter.Number,
                                    ["title"] = chapter.Title,
                                    ["summary"] = chapter.Summary,
                                }),
                            ]
                        ),
                    }),
                ]
            ),
        }.ToJsonString();

    private static string BoardKey(BoardType board) => board == BoardType.Ssc ? "ssc" : "cbse";

    private static string ClassKey(BoardType board, int classLevel) => $"{BoardKey(board)}-{classLevel}";
}
