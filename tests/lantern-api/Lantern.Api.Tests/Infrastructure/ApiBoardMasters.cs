using System.Globalization;
using System.Text;
using Azure.Storage.Blobs;

namespace Lantern.Api.Tests.Infrastructure;

/// <summary>
/// The Board masters the API tests copy from, under the container names in appsettings.Testing.json (the ones the in-process
/// hosts are configured with). Every Class a test can use has built Books: NCERT Classes 1 to 10 and SSC Classes 1 to 5, each
/// shaped as the real masters are (see <see cref="BoardMaster"/>), so registering or adding a Child gets a real slice.
/// The ids are written out here, never taken from the store's rules: NCERT Class c has Book <c>nbk{c}1</c> (Chapters 01 and 02)
/// and an unbuilt Hindi Book <c>nhi{c}1</c>; SSC Class c has Books <c>sbk{c}-maths</c> and <c>sbk{c}-english</c>.
/// </summary>
internal static class ApiBoardMasters
{
    public const int NcertClasses = 10;

    public const int SscClasses = 5;

    public static string NcertBook(int classLevel) => $"nbk{classLevel}1";

    public static string[] SscBooks(int classLevel) => [$"sbk{classLevel}-maths", $"sbk{classLevel}-english"];

    /// <summary>What a Child of this Class gets from NCERT, written out by hand, sorted.</summary>
    public static string[] NcertSlice(int classLevel) =>
    [
        "catalog.json",
        "manifest.json",
        $"refined/{NcertBook(classLevel)}/{NcertBook(classLevel)}01.json",
        $"refined/{NcertBook(classLevel)}/{NcertBook(classLevel)}02.json",
        $"summaries/{NcertBook(classLevel)}/{NcertBook(classLevel)}01.json",
        $"summaries/{NcertBook(classLevel)}/{NcertBook(classLevel)}02.json",
        "version.json",
    ];

    /// <summary>What a Child of this Class gets from SSC, written out by hand, sorted.</summary>
    public static string[] SscSlice(int classLevel)
    {
        var (english, maths) = (SscBooks(classLevel)[1], SscBooks(classLevel)[0]);

        return
        [
            "board.json",
            "catalog.json",
            $"guidance/{english}.json",
            $"guidance/{maths}.json",
            "manifest.json",
            $"refined/{english}/{english}-01.json",
            $"refined/{maths}/{maths}-01.json",
            $"summaries/{english}/{english}-01.json",
            $"summaries/{maths}/{maths}-01.json",
            $"toc/{english}.json",
            $"toc/{maths}.json",
            "version.json",
        ];
    }

    public static async Task SeedAsync(BlobServiceClient blobs)
    {
        var ncert = blobs.GetBlobContainerClient(TestSettings.NcertContainer);
        var ssc = blobs.GetBlobContainerClient(TestSettings.SscContainer);
        await ncert.CreateIfNotExistsAsync();
        await ssc.CreateIfNotExistsAsync();

        var ncertCatalog = new StringBuilder("[");
        var sscCatalog = new StringBuilder("[");
        for (var classLevel = 1; classLevel <= NcertClasses; classLevel++)
        {
            var book = NcertBook(classLevel);
            ncertCatalog.Append(CultureInfo.InvariantCulture, $$"""{"book_id":"{{book}}","grade":{{classLevel}},"medium":"English","first_chapter":1,"last_chapter":2},""");
            ncertCatalog.Append(CultureInfo.InvariantCulture, $$"""{"book_id":"nhi{{classLevel}}1","grade":{{classLevel}},"medium":"Hindi","first_chapter":1,"last_chapter":2},""");
            foreach (var chapter in new[] { "01", "02" })
            {
                foreach (var folder in new[] { "refined", "summaries", "text", "chapters", "drafts" })
                {
                    await PutAsync(ncert, $"{folder}/{book}/{book}{chapter}.json");
                }
            }
        }

        for (var classLevel = 1; classLevel <= SscClasses; classLevel++)
        {
            foreach (var book in SscBooks(classLevel))
            {
                sscCatalog.Append(CultureInfo.InvariantCulture, $$"""{"board":"ssc","book_id":"{{book}}","grade":{{classLevel}},"medium":"English","first_chapter":1,"last_chapter":1},""");
                foreach (var folder in new[] { "refined", "summaries", "text", "chapters", "drafts" })
                {
                    await PutAsync(ssc, $"{folder}/{book}/{book}-01.json");
                }

                await PutAsync(ssc, $"toc/{book}.json");
                await PutAsync(ssc, $"guidance/{book}.json");
            }
        }

        for (var classLevel = 1; classLevel <= NcertClasses; classLevel++)
        {
            await PutAsync(ncert, $"manifest/class-{classLevel}.json", NcertManifest(classLevel));
        }

        for (var classLevel = 1; classLevel <= SscClasses; classLevel++)
        {
            await PutAsync(ssc, $"manifest/class-{classLevel}.json", SscManifest(classLevel));
        }

        await PutAsync(ncert, "catalog.json", ncertCatalog.ToString().TrimEnd(',') + "]");
        await PutAsync(ssc, "catalog.json", sscCatalog.ToString().TrimEnd(',') + "]");
        await PutAsync(ssc, "board.json");
    }

    public static string NcertManifest(int classLevel) =>
        $$"""
        {"schema":"class-manifest-v1","board":"cbse","profileVersion":"cbse-v1","class":{{classLevel}},"subjects":[
          {"subjectId":"mathematics","name":"Mathematics","icon":"maths","chapterCount":2,"chapters":[
            {"chapterId":"{{NcertBook(classLevel)}}01","bookId":"{{NcertBook(classLevel)}}","number":1,"title":"One","summary":"About one."},
            {"chapterId":"{{NcertBook(classLevel)}}02","bookId":"{{NcertBook(classLevel)}}","number":2,"title":"Two","summary":"About two."}]}]}
        """;

    public static string SscManifest(int classLevel) =>
        $$"""
        {"schema":"class-manifest-v1","board":"ssc","profileVersion":"ssc-v2","class":{{classLevel}},"subjects":[
          {"subjectId":"mathematics","name":"Mathematics","icon":"maths","chapterCount":1,"chapters":[
            {"chapterId":"{{SscBooks(classLevel)[0]}}-01","bookId":"{{SscBooks(classLevel)[0]}}","number":1,"title":"One","summary":"About one."}]},
          {"subjectId":"english","name":"English","icon":"english","chapterCount":1,"chapters":[
            {"chapterId":"{{SscBooks(classLevel)[1]}}-01","bookId":"{{SscBooks(classLevel)[1]}}","number":1,"title":"One","summary":"About one."}]}]}
        """;

    private static async Task PutAsync(BlobContainerClient container, string name, string content = null) =>
        await container.GetBlobClient(name).UploadAsync(BinaryData.FromString(content ?? $"{name} :: {Guid.NewGuid():N}"), overwrite: true);
}
