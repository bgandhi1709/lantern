using Lantern.Api.Tests.Architecture;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Models;

namespace Lantern.Api.Tests.Manifests;

public sealed class ClassManifestTests
{
    private const string ClassThree = """
        {
          "schema": "class-manifest-v1",
          "board": "cbse",
          "profileVersion": "cbse-v1",
          "class": 3,
          "subjects": [
            {
              "subjectId": "english", "name": "English", "icon": "english", "chapterCount": 2,
              "chapters": [
                { "chapterId": "xen101", "bookId": "xen1", "number": 1, "title": "First English Chapter",  "summary": "A first summary." },
                { "chapterId": "xen102", "bookId": "xen1", "number": 2, "title": "Second English Chapter", "summary": "A second summary." }
              ]
            },
            {
              "subjectId": "mathematics", "name": "Mathematics", "icon": "maths", "chapterCount": 1,
              "chapters": [
                { "chapterId": "xma101", "bookId": "xma1", "number": 1, "title": "Counting", "summary": "Counting things." }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Parse_GivesTheClassTheProfileAndTheSubjectsWithoutTheirChapters()
    {
        var manifest = TestServices.ClassManifest(ClassThree);

        Assert.Equal(3, manifest.Class);
        Assert.Equal("cbse-v1", manifest.ProfileVersion);
        Assert.Equal(
            [("english", "English", "english", 2), ("mathematics", "Mathematics", "maths", 1)],
            manifest.Subjects.Select(subject => (subject.SubjectId, subject.Name, subject.Icon, subject.ChapterCount))
        );
    }

    [Fact]
    public void ChaptersOf_ASubject_ReturnsItsChaptersInTheOrderStored()
    {
        var chapters = TestServices.ClassManifest(ClassThree).ChaptersOf("english");

        Assert.Equal(
            [
                new Chapter("xen101", "xen1", 1, "First English Chapter", "A first summary."),
                new Chapter("xen102", "xen1", 2, "Second English Chapter", "A second summary."),
            ],
            chapters
        );
    }

    [Fact]
    public void ChaptersOf_ASubjectTheClassDoesNotHave_ReturnsNull() => Assert.Null(TestServices.ClassManifest(ClassThree).ChaptersOf("science"));

    [Fact]
    public void Parse_ACountThatIsNotTheNumberOfChapters_Throws() =>
        Assert.Throws<InvalidOperationException>(() => TestServices.ClassManifest(ClassThree.Replace("\"chapterCount\": 2", "\"chapterCount\": 3", StringComparison.Ordinal)));

    [Theory]
    [InlineData("class-manifest-v2")]
    [InlineData("family-manifest-v1")]
    [InlineData("")]
    public void Parse_ASchemaThisCodeDoesNotKnow_Throws(string schema) =>
        Assert.Throws<InvalidOperationException>(() => TestServices.ClassManifest(ClassThree.Replace("class-manifest-v1", schema, StringComparison.Ordinal)));

    [Fact]
    public void Parse_TheSampleNcertBuildWrites_IsReadAsTheBuildMeantIt()
    {
        var path = Path.Combine(LanternAssemblies.RepositoryRoot.FullName, "docs", "architecture", "contracts", "class-manifest.cbse-7.json");

        var manifest = TestServices.ClassManifest(File.ReadAllText(path));

        Assert.Equal(7, manifest.Class);
        Assert.Equal("cbse-v1", manifest.ProfileVersion);
        Assert.Equal(
            [("mathematics", "Mathematics", "maths", 3), ("english", "English", "english", 2), ("art", "Art", "book", 1)],
            manifest.Subjects.Select(subject => (subject.SubjectId, subject.Name, subject.Icon, subject.ChapterCount))
        );
        Assert.Equal(
            [
                new Chapter("gegp101", "gegp1", 1, "Integers", "About gegp101."),
                new Chapter("gegp102", "gegp1", 2, "Fractions", "About gegp102."),
                new Chapter("gegp201", "gegp2", 1, "Lines", "About gegp201."),
            ],
            manifest.ChaptersOf("mathematics")
        );
    }
}
