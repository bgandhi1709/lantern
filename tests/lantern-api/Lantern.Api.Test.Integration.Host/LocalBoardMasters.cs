using System.Globalization;
using System.Text;
using Azure.Storage.Blobs;
using Lantern.Core.Configuration;

namespace Lantern.Api.Test.Integration.Host;

// Local Docker only. The Boards' containers in Azurite, with synthetic content shaped like the real ncert and ssc
// containers (never publisher text, ADR-0009): NCERT Classes 1 to 10, each with one built Book nbk{c}1 (Chapters 01 and 02)
// and an unbuilt Hindi Book; SSC Classes 1 to 5, each with Books sbk{c}-maths and sbk{c}-english, plus board.json, a toc
// and guidance per Book. Both carry the build leftovers a Child must never be given. Safe to run again.
internal static class LocalBoardMasters
{
    public static async Task SeedAsync(BlobServiceClient blobs, CatalogOptions catalog, CancellationToken cancellationToken)
    {
        var ncert = blobs.GetBlobContainerClient(catalog.NcertContainer);
        var ssc = blobs.GetBlobContainerClient(catalog.SscContainer);
        await ncert.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await ssc.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var ncertCatalog = new StringBuilder();
        for (var classLevel = 1; classLevel <= 10; classLevel++)
        {
            var book = $"nbk{classLevel}1";
            Entry(ncertCatalog, $$"""{"book_id":"{{book}}","grade":{{classLevel}},"medium":"English","first_chapter":1,"last_chapter":2}""");
            Entry(ncertCatalog, $$"""{"book_id":"nhi{{classLevel}}1","grade":{{classLevel}},"medium":"Hindi","first_chapter":1,"last_chapter":2}""");
            foreach (var chapter in new[] { "01", "02" })
            {
                foreach (var folder in new[] { "refined", "summaries", "text", "chapters", "drafts" })
                {
                    await PutAsync(ncert, $"{folder}/{book}/{book}{chapter}.json", null, cancellationToken);
                }
            }
        }

        var sscCatalog = new StringBuilder();
        for (var classLevel = 1; classLevel <= 5; classLevel++)
        {
            foreach (var book in new[] { $"sbk{classLevel}-maths", $"sbk{classLevel}-english" })
            {
                Entry(sscCatalog, $$"""{"board":"ssc","book_id":"{{book}}","grade":{{classLevel}},"medium":"English","first_chapter":1,"last_chapter":1}""");
                foreach (var folder in new[] { "refined", "summaries", "text", "chapters", "drafts" })
                {
                    await PutAsync(ssc, $"{folder}/{book}/{book}-01.json", null, cancellationToken);
                }

                await PutAsync(ssc, $"toc/{book}.json", null, cancellationToken);
                await PutAsync(ssc, $"guidance/{book}.json", null, cancellationToken);
            }
        }

        await PutAsync(ncert, "catalog.json", $"[{ncertCatalog}]", cancellationToken);
        await PutAsync(ssc, "catalog.json", $"[{sscCatalog}]", cancellationToken);
        await PutAsync(ssc, "board.json", """{"board":"ssc","classes":[1,2,3,4,5],"medium":"English"}""", cancellationToken);
    }

    private static void Entry(StringBuilder catalog, string json) => catalog.Append(catalog.Length == 0 ? string.Empty : ",").Append(json);

    private static async Task PutAsync(BlobContainerClient container, string name, string content, CancellationToken cancellationToken) =>
        await container
            .GetBlobClient(name)
            .UploadAsync(
                BinaryData.FromString(content ?? string.Create(CultureInfo.InvariantCulture, $"{name} :: {Guid.NewGuid():N}")),
                overwrite: true,
                cancellationToken
            );
}
