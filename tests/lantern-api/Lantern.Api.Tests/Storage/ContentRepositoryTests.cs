using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Repository;

namespace Lantern.Api.Tests.Storage;

[Collection(ApiCollection.Name)]
public sealed class ContentRepositoryTests(CosmosFixture cosmos)
{
    private static readonly Chapter[] English =
    [
        new("ssc1-english-01", "ssc1-english", 1, "Unit One", "Rhymes, letters and greetings."),
        new("ssc1-english-02", "ssc1-english", 2, "Unit Two", "Colours, numbers and the family."),
    ];

    private static readonly Subject EnglishSubject = new("english", "English", "english", English.Length);

    private ContentRepository Repository => new(cosmos.Content, TestServices.Json, TestServices.Layout);

    [Fact]
    public async Task ClassAsync_AClassThatIsLoaded_ReturnsItsManifest()
    {
        await ContentSeed.PutClassAsync(cosmos.Content, BoardType.Ssc, 1, (EnglishSubject, English));

        var manifest = await Repository.ClassAsync(BoardType.Ssc, 1, CancellationToken.None);

        Assert.Equal(1, manifest.Class);
        var subject = Assert.Single(manifest.Subjects);
        Assert.Equal(("english", "English", "english", 2), (subject.SubjectId, subject.Name, subject.Icon, subject.ChapterCount));
        Assert.Equal(English, subject.Chapters);
    }

    [Fact]
    public async Task ClassAsync_AClassWithNoClassDocument_ReturnsNull()
    {
        await ContentSeed.RemoveClassAsync(cosmos.Content, BoardType.Cbse, 9);

        Assert.Null(await Repository.ClassAsync(BoardType.Cbse, 9, CancellationToken.None));
    }

    [Fact]
    public async Task ClassAsync_ReadsOnlyTheBoardsOwnClass()
    {
        await ContentSeed.PutClassAsync(cosmos.Content, BoardType.Cbse, 4, (EnglishSubject, English));
        await ContentSeed.RemoveClassAsync(cosmos.Content, BoardType.Ssc, 4);

        Assert.Null(await Repository.ClassAsync(BoardType.Ssc, 4, CancellationToken.None));
    }

    [Fact]
    public async Task ClassAsync_ASchemaItDoesNotKnow_Throws_NeverAnEmptyAnswer()
    {
        await ContentSeed.PutClassRawAsync(cosmos.Content, BoardType.Cbse, 10, ContentSeed.ManifestJson("class-manifest-v9", "cbse", 10, (EnglishSubject, English)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Repository.ClassAsync(BoardType.Cbse, 10, CancellationToken.None));
    }

    [Fact]
    public async Task ClassAsync_ASubjectWhoseCountDoesNotMatchItsChapters_Throws()
    {
        var manifest = ContentSeed.ManifestJson("class-manifest-v1", "cbse", 2, (EnglishSubject, English)).Replace("\"chapterCount\":2", "\"chapterCount\":5", StringComparison.Ordinal);
        await ContentSeed.PutClassRawAsync(cosmos.Content, BoardType.Cbse, 2, manifest);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Repository.ClassAsync(BoardType.Cbse, 2, CancellationToken.None));
    }

    [Fact]
    public async Task ClassAsync_ADocumentWithNoManifest_Throws()
    {
        await ContentSeed.PutClassRawAsync(cosmos.Content, BoardType.Ssc, 5, "null");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Repository.ClassAsync(BoardType.Ssc, 5, CancellationToken.None));
    }
}
