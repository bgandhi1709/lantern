using System.Text.Json.Nodes;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Models;

namespace Lantern.Api.Tests.Manifests;

public sealed class FamilyManifestTests
{
    private static readonly Guid Rohan = Guid.Parse("485c8356-9095-4cb7-a1cd-3228dad1fbb5");

    private const string RohanOnly = """
        {
          "schema": "family-manifest-v1",
          "board": "cbse",
          "children": {
            "485c8356-9095-4cb7-a1cd-3228dad1fbb5": {
              "class": 3,
              "profileVersion": "cbse-v1",
              "subjects": [
                { "subjectId": "english",             "name": "English",             "icon": "english", "chapterCount": 12 },
                { "subjectId": "mathematics",         "name": "Mathematics",         "icon": "maths",   "chapterCount": 14 },
                { "subjectId": "the-world-around-us", "name": "The World Around Us", "icon": "evs",     "chapterCount": 12 }
              ]
            }
          }
        }
        """;

    [Fact]
    public void SubjectsOf_AChildWithAnEntry_ReturnsItsSubjectsInTheOrderStored()
    {
        var subjects = TestServices.FamilyManifest(RohanOnly).SubjectsOf(Rohan);

        Assert.Equal(
            [
                new Subject("english", "English", "english", 12),
                new Subject("mathematics", "Mathematics", "maths", 14),
                new Subject("the-world-around-us", "The World Around Us", "evs", 12),
            ],
            subjects
        );
    }

    [Fact]
    public void SubjectsOf_AChildWithNoEntry_ReturnsNull_SoTheCallerCanSayNotReady() =>
        Assert.Null(TestServices.FamilyManifest(RohanOnly).SubjectsOf(Guid.Parse("11111111-1111-1111-1111-111111111111")));

    [Theory]
    [InlineData("family-manifest-v2")]
    [InlineData("class-manifest-v1")]
    [InlineData("")]
    public void Parse_ASchemaThisCodeDoesNotKnow_ThrowsInsteadOfReadingItAsEmpty(string schema) =>
        Assert.Throws<InvalidOperationException>(() => TestServices.FamilyManifest($$"""{ "schema": "{{schema}}", "board": "cbse", "children": {} }"""));

    [Fact]
    public void Parse_NoSchemaAtAll_Throws() =>
        Assert.Throws<InvalidOperationException>(() => TestServices.FamilyManifest("""{ "board": "cbse", "children": {} }"""));

    private static readonly Guid Mira = Guid.Parse("21343db4-d6d6-487e-9d0d-598e77b45935");

    private static readonly Subject[] ClassSix = [new("science", "Science", "science", 12), new("mathematics", "Mathematics", "maths", 10)];

    [Fact]
    public void Empty_IsAFamilyWithNoChildrenYet_AndKeepsTheBoard() =>
        AssertJson("""{ "schema": "family-manifest-v1", "board": "cbse", "children": {} }""", TestServices.Json.Serialize(FamilyManifest.Empty("family-manifest-v1", "cbse")));

    [Fact]
    public void Add_PutsTheChildsEntryInTheShapeTheReaderExpects()
    {
        var manifest = FamilyManifest.Empty("family-manifest-v1", "cbse");

        manifest.Add(Mira, Entry(6, "cbse-v1", ClassSix));

        AssertJson(
            """
            {
              "schema": "family-manifest-v1",
              "board": "cbse",
              "children": {
                "21343db4-d6d6-487e-9d0d-598e77b45935": {
                  "class": 6,
                  "profileVersion": "cbse-v1",
                  "subjects": [
                    { "subjectId": "science",     "name": "Science",     "icon": "science", "chapterCount": 12 },
                    { "subjectId": "mathematics", "name": "Mathematics", "icon": "maths",   "chapterCount": 10 }
                  ]
                }
              }
            }
            """,
            TestServices.Json.Serialize(manifest)
        );
    }

    [Fact]
    public void Add_AnotherChild_KeepsTheFirstChildsEntry()
    {
        var manifest = TestServices.FamilyManifest(RohanOnly);

        manifest.Add(Mira, Entry(6, "cbse-v1", ClassSix));

        Assert.Equal(ClassSix, manifest.SubjectsOf(Mira));
        Assert.Equal(3, manifest.SubjectsOf(Rohan).Count);
    }

    [Fact]
    public void Has_TheSameEntry_IsTrue_ButAChangedClassOrCountOrProfileIsNot()
    {
        var manifest = FamilyManifest.Empty("family-manifest-v1", "cbse");
        manifest.Add(Mira, Entry(6, "cbse-v1", ClassSix));

        Assert.True(manifest.Has(Mira, Entry(6, "cbse-v1", ClassSix)));
        Assert.False(manifest.Has(Mira, Entry(7, "cbse-v1", ClassSix)));
        Assert.False(manifest.Has(Mira, Entry(6, "cbse-v2", ClassSix)));
        Assert.False(manifest.Has(Mira, Entry(6, "cbse-v1", [new("science", "Science", "science", 13), ClassSix[1]])));
        Assert.False(manifest.Has(Rohan, Entry(6, "cbse-v1", ClassSix)));
    }

    [Fact]
    public void Remove_TakesOutOnlyThatChild_AndAChildWithNoEntryChangesNothing()
    {
        var manifest = TestServices.FamilyManifest(RohanOnly);
        manifest.Add(Mira, Entry(6, "cbse-v1", ClassSix));
        var before = TestServices.Json.Serialize(manifest);

        Assert.False(manifest.Remove(Guid.NewGuid()));
        AssertJson(before, TestServices.Json.Serialize(manifest));

        Assert.True(manifest.Remove(Rohan));
        Assert.Null(manifest.SubjectsOf(Rohan));
        Assert.Equal(ClassSix, manifest.SubjectsOf(Mira));
    }

    private static FamilyChild Entry(int classLevel, string profileVersion, Subject[] subjects) =>
        new() { Class = classLevel, ProfileVersion = profileVersion, Subjects = [.. subjects] };

    private static void AssertJson(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"Expected {expected}{Environment.NewLine}Actual {actual}");
}
