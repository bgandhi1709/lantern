namespace Lantern.Api.Tests.Architecture;

public sealed class CryptoTests
{
    private static readonly string[] Allowed = ["CryptoService.cs", "OneTimeCodeGenerator.cs"];

    private static readonly string[] Folders = ["apps", "libs"];

    [Fact]
    public void CryptographyPrimitives_AreUsedOnlyByTheCryptoServiceAndTheCodeGenerator()
    {
        var root = LanternAssemblies.RepositoryRoot.FullName;
        var offenders =
            from folder in Folders
            from file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)
            where !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            where !Allowed.Contains(Path.GetFileName(file))
            where File.ReadAllText(file).Contains("System.Security.Cryptography", StringComparison.Ordinal)
            select Path.GetRelativePath(root, file);

        Assert.Empty(offenders);
    }
}
