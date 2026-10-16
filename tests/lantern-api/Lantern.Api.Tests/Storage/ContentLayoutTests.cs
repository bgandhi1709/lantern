using Lantern.Core.Storage;

namespace Lantern.Api.Tests.Storage;

// The names are the contract with ncert-build and with what is already stored, so they are written out here as literals: a test
// that read them from the class under test would agree with any change.
public sealed class ContentLayoutTests
{
    private static readonly Guid Family = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Child = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly ContentLayout layout = new();

    [Fact]
    public void TheFileNames_AreTheOnesAlreadyStoredAndBuilt()
    {
        Assert.Equal("class.json", layout.MarkerFile);
        Assert.Equal("catalog.json", layout.CatalogFile);
        Assert.Equal("manifest.json", layout.ClassManifestFile);
        Assert.Equal("manifest.json", layout.FamilyManifestFile);
        Assert.Equal("version.json", layout.VersionFile);
        Assert.Equal("board.json", layout.BoardProfileFile);
        Assert.Equal("toc", layout.TocFolder);
        Assert.Equal("guidance", layout.GuidanceFolder);
    }

    [Fact]
    public void TheSchemaIdsAndCatalogFields_AreTheOnesTheBuildWrites()
    {
        Assert.Equal("class-manifest-v1", layout.ClassManifestSchema);
        Assert.Equal("family-manifest-v1", layout.FamilyManifestSchema);
        Assert.Equal("book_id", layout.CatalogBookField);
        Assert.Equal("grade", layout.CatalogGradeField);
    }

    [Fact]
    public void WorkspacePaths_NestFamilyChildAndClass()
    {
        Assert.Equal("11111111-1111-1111-1111-111111111111/", layout.FamilyFolder(Family));
        Assert.Equal("11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/", layout.ChildFolder(Family, Child));
        Assert.Equal("11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/3/class.json", layout.MarkerPath(Family, Child, 3));
        Assert.Equal("11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/3/content/", layout.ContentFolder(Family, Child, 3));
        Assert.Equal("11111111-1111-1111-1111-111111111111/manifest.json", layout.FamilyManifestPath(Family));
    }

    [Fact]
    public void MasterPaths_FollowTheBoardLayoutTheBuildWrites()
    {
        Assert.Equal("manifest/class-7.json", layout.MasterClassManifest(7));
        Assert.Equal("refined/cesa1/", layout.RefinedFolder("cesa1"));
        Assert.Equal("summaries/cesa1/", layout.SummariesFolder("cesa1"));
        Assert.Equal("summaries/cesa1/cesa101.json", layout.SummaryOf("refined/cesa1/cesa101.json"));
        Assert.Equal("toc/ssc3-maths.json", layout.BookFile("toc", "ssc3-maths"));
    }
}
