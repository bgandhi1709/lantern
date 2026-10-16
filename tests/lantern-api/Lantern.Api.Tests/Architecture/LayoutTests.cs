namespace Lantern.Api.Tests.Architecture;

// The Workspace and Board master names live in ContentLayout alone: a file or folder name written anywhere else in production code
// is a second copy that can drift from the one ncert-build writes.
public sealed class LayoutTests
{
    private static readonly string[] Folders = ["apps", "libs"];

    private static readonly string[] Forbidden = [".json\"", "\"refined/", "\"summaries/", "\"toc\"", "\"guidance\"", "manifest-v1\""];

    [Fact]
    public void ProductionCode_NamesWorkspaceFilesAndFolders_OnlyInContentLayout()
    {
        var root = LanternAssemblies.RepositoryRoot.FullName;
        var offenders =
            from folder in Folders
            from file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)
            where !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            where !file.EndsWith($"{Path.DirectorySeparatorChar}ContentLayout.cs", StringComparison.Ordinal)
            let text = File.ReadAllText(file)
            from token in Forbidden
            where text.Contains(token, StringComparison.Ordinal)
            select $"{Path.GetRelativePath(root, file)} uses {token}";

        Assert.Empty(offenders);
    }
}
