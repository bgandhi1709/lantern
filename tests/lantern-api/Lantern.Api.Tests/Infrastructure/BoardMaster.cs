using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Lantern.Api.Tests.Infrastructure;

/// <summary>
/// A synthetic Board master container shaped like the real <c>ncert</c> and <c>ssc</c> ones (checked against UAT's listing),
/// never publisher text (ADR-0009). Every file's bytes are distinct, so an equality check means the right file was copied.
/// Each master carries what the build leaves behind (text, chapters, drafts, bundles, pictures, review, logs), the Books
/// of other Classes, an id that is a prefix of another (<c>cxyz1</c> and <c>cxyz10</c>), and Books whose files are not built.
/// </summary>
internal sealed class BoardMaster(BlobContainerClient container)
{
    public BlobContainerClient Container { get; } = container;

    public static async Task<BoardMaster> NcertAsync(AzuriteFixture azurite)
    {
        var master = await NewAsync(azurite, "ncert");

        // NCERT lists every Book in every medium: only the built ones can be served. It has no board key, board.json, toc or guidance.
        await master.PutAsync(
            "catalog.json",
            """
            [
              {"book_id":"cxyz1","grade":3,"subject":"English","title":"Test Reader","medium":"English","first_chapter":1,"last_chapter":2,"has_front_matter":true,"future_field":{"keep":["me",1]}},
              {"book_id":"cxyz10","grade":4,"subject":"English","title":"Prefix Trap","medium":"English","first_chapter":1,"last_chapter":1,"has_front_matter":true},
              {"book_id":"cmat1","grade":3,"subject":"Mathematics","title":"Test Maths","medium":"English","first_chapter":1,"last_chapter":2,"has_front_matter":false},
              {"book_id":"chin1","grade":3,"subject":"Hindi","title":"Unbuilt Hindi","medium":"Hindi","first_chapter":1,"last_chapter":2,"has_front_matter":false},
              {"book_id":"curd1","grade":3,"subject":"Urdu","title":"Unbuilt Urdu","medium":"Urdu","first_chapter":1,"last_chapter":2,"has_front_matter":false},
              {"book_id":"dxyz1","grade":4,"subject":"English","title":"Class Four","medium":"English","first_chapter":1,"last_chapter":1,"has_front_matter":true}
            ]
            """
        );

        // cmat1 declares two Chapters but only the first is built, as NCERT's jewe2 is in the real master.
        foreach (var chapter in new[] { "cxyz1/cxyz101", "cxyz1/cxyz102", "cmat1/cmat101", "cxyz10/cxyz1001", "dxyz1/dxyz101" })
        {
            await master.PutAsync($"refined/{chapter}.json");
            await master.PutAsync($"summaries/{chapter}.json");
        }

        // Build leftovers that must never reach a Child, for the Books of Class 3 and the others.
        foreach (var chapter in new[] { "cxyz1/cxyz101", "cxyz1/cxyz102", "cmat1/cmat101", "cmat1/cmat102", "cxyz10/cxyz1001", "dxyz1/dxyz101" })
        {
            await master.PutAsync($"text/{chapter}.json");
            await master.PutAsync($"chapters/{chapter}.json");
            await master.PutAsync($"drafts/{chapter}.json");
            await master.PutAsync($"bundles/{chapter}.md");
            await master.PutAsync($"pictures/{chapter}.json");
            await master.PutAsync($"pictures/{chapter}/p1-0.png");
        }

        await master.PutAsync("review/report-3.txt");
        await master.PutAsync("logs/run-1.log");

        // NCERT has none of these; a stray one must not be mistaken for content.
        await master.PutAsync("board.json");
        await master.PutAsync("toc/cxyz1.json");
        await master.PutAsync("guidance/cxyz1.json");

        return master;
    }

    public static async Task<BoardMaster> SscAsync(AzuriteFixture azurite)
    {
        var master = await NewAsync(azurite, "ssc");

        await master.PutAsync(
            "catalog.json",
            """
            [
              {"board":"ssc","book_id":"ssc3-maths","grade":3,"subject":"Mathematics","title":"Mathematics Standard 3","medium":"English","first_chapter":1,"last_chapter":2,"part":null,"edition":"2026","pdf_url":"https://example.invalid/m3.pdf","source_item_id":"11","source_sha256":"aa"},
              {"board":"ssc","book_id":"ssc3-english","grade":3,"subject":"English","title":"English Balbharati Standard 3","medium":"English","first_chapter":1,"last_chapter":1,"part":null,"edition":"2026","pdf_url":"https://example.invalid/e3.pdf","source_item_id":"12","source_sha256":"bb"},
              {"board":"ssc","book_id":"ssc3-evs1","grade":3,"subject":"Environmental Studies","title":"The World Around Us Part 1","medium":"English","first_chapter":1,"last_chapter":1,"part":1,"edition":"2026","pdf_url":"https://example.invalid/v3.pdf","source_item_id":"13","source_sha256":"cc"},
              {"board":"ssc","book_id":"ssc3-evs12","grade":4,"subject":"Environmental Studies","title":"Prefix Trap","medium":"English","first_chapter":1,"last_chapter":1,"part":2,"edition":"2026","pdf_url":"https://example.invalid/t.pdf","source_item_id":"14","source_sha256":"dd"},
              {"board":"ssc","book_id":"ssc4-maths","grade":4,"subject":"Mathematics","title":"Mathematics Standard 4","medium":"English","first_chapter":1,"last_chapter":1,"part":null,"edition":"2026","pdf_url":"https://example.invalid/m4.pdf","source_item_id":"15","source_sha256":"ee"}
            ]
            """
        );
        await master.PutAsync("board.json", """{"board":"ssc","classes":[1,2,3,4,5],"medium":"English"}""");

        string[] chapters =
        [
            "ssc3-maths/ssc3-maths-01",
            "ssc3-maths/ssc3-maths-02",
            "ssc3-english/ssc3-english-01",
            "ssc3-evs1/ssc3-evs1-01",
            "ssc3-evs12/ssc3-evs12-01",
            "ssc4-maths/ssc4-maths-01",
        ];
        foreach (var chapter in chapters)
        {
            await master.PutAsync($"refined/{chapter}.json");
            await master.PutAsync($"summaries/{chapter}.json");
            await master.PutAsync($"text/{chapter}.json");
            await master.PutAsync($"chapters/{chapter}.json");
            await master.PutAsync($"drafts/{chapter}.json");
            await master.PutAsync($"bundles/{chapter}.md");
            await master.PutAsync($"pictures/{chapter}.json");
            await master.PutAsync($"pictures/{chapter}/p1-0.png");
        }

        foreach (var book in new[] { "ssc3-maths", "ssc3-english", "ssc3-evs1", "ssc3-evs12", "ssc4-maths" })
        {
            await master.PutAsync($"toc/{book}.json");
            await master.PutAsync($"guidance/{book}.json");
        }

        await master.PutAsync("review/report-3.txt");
        await master.PutAsync("logs/run-1.log");

        return master;
    }

    /// <summary>Writes a blob; without content, its bytes are its own name plus a fresh id, so no two blobs are alike.</summary>
    public async Task PutAsync(string name, string content = null)
    {
        var text = content ?? $"{name} :: {Guid.NewGuid():N} :: नमस्ते";
        await Container.GetBlobClient(name).UploadAsync(BinaryData.FromBytes(Encoding.UTF8.GetBytes(text)), overwrite: true);
    }

    public Task RemoveAsync(string name) => Container.GetBlobClient(name).DeleteIfExistsAsync();

    public async Task<byte[]> BytesAsync(string name) =>
        (await Container.GetBlobClient(name).DownloadContentAsync()).Value.Content.ToArray();

    /// <summary>Every blob and its ETag, to show the master is unchanged by a copy.</summary>
    public async Task<IReadOnlyDictionary<string, string>> SnapshotAsync()
    {
        var all = new SortedDictionary<string, string>(StringComparer.Ordinal);
        await foreach (var blob in Container.GetBlobsAsync(BlobTraits.None, BlobStates.None, string.Empty, CancellationToken.None))
        {
            all[blob.Name] = blob.Properties.ETag?.ToString();
        }

        return all;
    }

    private static async Task<BoardMaster> NewAsync(AzuriteFixture azurite, string board)
    {
        var container = azurite.CreateBlobClient().GetBlobContainerClient($"{board}{Guid.NewGuid():N}");
        await container.CreateAsync();

        return new BoardMaster(container);
    }
}
