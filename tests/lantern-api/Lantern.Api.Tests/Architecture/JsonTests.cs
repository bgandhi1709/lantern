namespace Lantern.Api.Tests.Architecture;

// One JSON shape for the whole app: production code reads and writes JSON only through IJsonService, so the options live in one
// place and a change to them is a change to every document at once.
public sealed class JsonTests
{
    private static readonly string[] Forbidden =
    [
        "JsonSerializer.",
        "JsonNode.Parse",
        "JsonObject.Parse",
        "JsonSerializerOptions.Web",
        "new JsonSerializerOptions",
        "JsonSerializerDefaults",
        "ToObjectFromJson",
        "FromObjectAsJson",
    ];

    private static readonly string[] Folders = ["apps", "libs"];

    [Fact]
    public void ProductionCode_ReadsAndWritesJson_OnlyThroughTheJsonService()
    {
        var root = LanternAssemblies.RepositoryRoot.FullName;
        var offenders =
            from folder in Folders
            from file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)
            where !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            where !file.EndsWith($"{Path.DirectorySeparatorChar}JsonService.cs", StringComparison.Ordinal)
            let text = File.ReadAllText(file)
            from token in Forbidden
            where text.Contains(token, StringComparison.Ordinal)
            select $"{Path.GetRelativePath(root, file)} uses {token}";

        Assert.Empty(offenders);
    }
}
