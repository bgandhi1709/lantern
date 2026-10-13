using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure;
using Lantern.Core.Constants;
using Lantern.Core.Repository;

namespace Lantern.Repository;

// Blob has no empty folders, so a Workspace exists once its marker blob does. createContainer is for Azurite
// only; in Azure the Bicep owns the container. `boards` holds each Board's master container, which only the Content slice
// (ADR-0013) reads.
internal sealed partial class WorkspaceStore(
    BlobContainerClient container,
    IReadOnlyDictionary<BoardType, BlobContainerClient> boards,
    TimeProvider clock,
    bool createContainer
) : IWorkspaceStore
{
    private const string MarkerName = "class.json";

    private const int MaxParallelDeletes = 16;

    private const string CatalogName = "catalog.json";

    private const string VersionName = "version.json";

    private const int MaxParallelCopies = 16;

    // Caps Blob requests in flight across every copy in the process; the Azure SDK retries throttling with backoff.
    private const int MaxCopiesInFlight = 32;

    private static readonly SemaphoreSlim InFlight = new(MaxCopiesInFlight, MaxCopiesInFlight);

    // What each Board's master holds, checked against UAT's `ncert` and `ssc` (ADR-0013).
    private static readonly Dictionary<BoardType, BoardRules> Rules = new()
    {
        [BoardType.Cbse] = new(SkipsUnbuiltBooks: true, BoardFiles: [], BookFiles: []),
        [BoardType.Ssc] = new(SkipsUnbuiltBooks: false, BoardFiles: ["board.json"], BookFiles: ["toc", "guidance"]),
    };

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private sealed record BoardRules(bool SkipsUnbuiltBooks, string[] BoardFiles, string[] BookFiles);

    // One Book's entry and the master blobs it needs, each with its ETag.
    private sealed record BookManifest(string Book, JsonNode Entry, Dictionary<string, ETag> Files);

    // Snapshot: every master blob the manifest read, with its ETag, to check the master did not change during the copy.
    private sealed record ContentManifest(IReadOnlyList<string> Files, byte[] Catalog, IReadOnlyList<string> Books, IReadOnlyDictionary<string, ETag> Snapshot);

    public async Task CreateAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken)
    {
        if (createContainer)
        {
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        var path = string.Create(CultureInfo.InvariantCulture, $"{familyId:D}/{childId:D}/{classLevel}/{MarkerName}");
        var options = new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } };

        try
        {
            await container.GetBlobClient(path).UploadAsync(BinaryData.Empty, options, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists)
        {
            // Repeated Class: the space already exists.
        }
    }

    public async Task CopyContentAsync(Guid familyId, Guid childId, int classLevel, BoardType board, CancellationToken cancellationToken)
    {
        var prefix = string.Create(CultureInfo.InvariantCulture, $"{familyId:D}/{childId:D}/{classLevel}/content/");
        var version = container.GetBlobClient(prefix + VersionName);

        if (await version.ExistsAsync(cancellationToken))
        {
            return;
        }

        var master = boards.TryGetValue(board, out var found)
            ? found
            : throw new InvalidOperationException($"No master container is set for the {board} Board.");
        var manifest = await ReadManifestAsync(master, Rules[board], classLevel, cancellationToken);

        await CopyFilesAsync(master, manifest, prefix, cancellationToken);
        await RemoveLeftoversAsync(manifest, prefix, cancellationToken);

        // No lock on the master (a lease would make every Family wait on one writer): the manifest read again must match the first.
        var secondManifest = await ReadManifestAsync(master, Rules[board], classLevel, cancellationToken);
        if (!SameSnapshot(manifest.Snapshot, secondManifest.Snapshot))
        {
            throw new InvalidOperationException($"The {board} Board's master changed while Class {classLevel} was being copied.");
        }

        // Last, so a half copy is never ready.
        var made = new JsonObject
        {
            ["board"] = board.ToString().ToLowerInvariant(),
            ["class"] = classLevel,
            ["books"] = new JsonArray([.. manifest.Books.Select(book => (JsonNode)JsonValue.Create(book))]),
            ["files"] = manifest.Files.Count + 1,
            ["copiedAt"] = clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
        };
        await version.UploadAsync(BinaryData.FromString(made.ToJsonString(Pretty)), overwrite: true, cancellationToken);
    }

    private async Task CopyFilesAsync(BlobContainerClient master, ContentManifest manifest, string prefix, CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(
            manifest.Files,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelCopies, CancellationToken = cancellationToken },
            async (name, token) =>
            {
                await InFlight.WaitAsync(token);
                try
                {
                    // Whole files, one Put Blob each: a stream of unknown length is staged in blocks, and two copies at once fail.
                    var source = await master.GetBlobClient(name).DownloadContentAsync(token);
                    var options = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = source.Value.Details.ContentType } };
                    await container.GetBlobClient(prefix + name).UploadAsync(source.Value.Content, options, token);
                }
                finally
                {
                    InFlight.Release();
                }
            }
        );
        await container.GetBlobClient(prefix + CatalogName).UploadAsync(BinaryData.FromBytes(manifest.Catalog), overwrite: true, cancellationToken);
    }

    // version.json is never touched: a copy running beside this one may have just written it.
    private async Task RemoveLeftoversAsync(ContentManifest manifest, string prefix, CancellationToken cancellationToken)
    {
        var wanted = new HashSet<string>([.. manifest.Files, CatalogName, VersionName], StringComparer.Ordinal);
        var leftovers = (await ListAsync(container, prefix, cancellationToken)).Keys.Where(name => !wanted.Contains(name[prefix.Length..]));

        await Parallel.ForEachAsync(
            leftovers,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDeletes, CancellationToken = cancellationToken },
            async (name, token) => await container.DeleteBlobIfExistsAsync(name, cancellationToken: token)
        );
    }

    // Everything is checked before the first write, so a missing file leaves nothing behind.
    private static async Task<ContentManifest> ReadManifestAsync(BlobContainerClient master, BoardRules rules, int classLevel, CancellationToken cancellationToken)
    {
        var catalogRead = (await master.GetBlobClient(CatalogName).DownloadContentAsync(cancellationToken)).Value;
        var catalog = JsonNode.Parse(catalogRead.Content) as JsonArray ?? throw new InvalidOperationException("The Board's catalog is not a list of Books.");

        // A Class has at most a handful of Books, so they are read together.
        var entries = catalog.OfType<JsonObject>().Where(entry => IsGrade(entry, classLevel));
        var manifests = (await Task.WhenAll(entries.Select(entry => ReadBookManifestAsync(master, rules, entry, classLevel, cancellationToken)))).Where(book => book is not null).ToList();
        if (manifests.Count == 0)
        {
            throw new InvalidOperationException($"The Board has no built Book for Class {classLevel}.");
        }

        var snapshot = new Dictionary<string, ETag>(StringComparer.Ordinal) { [CatalogName] = catalogRead.Details.ETag };
        foreach (var (name, etag) in manifests.SelectMany(book => book.Files))
        {
            snapshot[name] = etag;
        }

        foreach (var (name, etag) in await Task.WhenAll(rules.BoardFiles.Select(name => RequiredAsync(master, name, cancellationToken))))
        {
            snapshot[name] = etag;
        }

        var kept = new JsonArray([.. manifests.Select(book => book.Entry.DeepClone())]);
        string[] files = [.. snapshot.Keys.Where(name => name != CatalogName).Order(StringComparer.Ordinal)];

        return new ContentManifest(files, JsonSerializer.SerializeToUtf8Bytes(kept, Pretty), [.. manifests.Select(book => book.Book)], snapshot);
    }

    // Null for a Book NCERT lists and has not built; for SSC an unbuilt Book, or a missing file, is a broken master.
    private static async Task<BookManifest> ReadBookManifestAsync(
        BlobContainerClient master,
        BoardRules rules,
        JsonObject entry,
        int classLevel,
        CancellationToken cancellationToken
    )
    {
        var book = entry["book_id"]?.GetValue<string>();
        if (book is null || !BookId().IsMatch(book))
        {
            throw new InvalidOperationException("A catalog entry has no usable book_id.");
        }

        var refinedTask = ListAsync(master, $"refined/{book}/", cancellationToken);
        var summariesTask = ListAsync(master, $"summaries/{book}/", cancellationToken);
        var (refined, summaries) = (await refinedTask, await summariesTask);

        if (refined.Count == 0)
        {
            return rules.SkipsUnbuiltBooks
                ? null
                : throw new InvalidOperationException($"The Board lists Book {book} for Class {classLevel} but has no refined content for it.");
        }

        var unsummarised = refined.Keys.FirstOrDefault(chapter => !summaries.ContainsKey("summaries/" + chapter["refined/".Length..]));
        if (unsummarised is not null)
        {
            throw new InvalidOperationException($"Book {book} has refined content with no summary: {unsummarised}.");
        }

        var files = new Dictionary<string, ETag>(refined.Concat(summaries), StringComparer.Ordinal);
        foreach (var (name, etag) in await Task.WhenAll(rules.BookFiles.Select(folder => RequiredAsync(master, $"{folder}/{book}.json", cancellationToken))))
        {
            files[name] = etag;
        }

        return new BookManifest(book, entry, files);
    }

    private static bool IsGrade(JsonObject entry, int classLevel) =>
        entry["grade"] is JsonValue grade && grade.TryGetValue<int>(out var value) && value == classLevel;

    private static bool SameSnapshot(IReadOnlyDictionary<string, ETag> first, IReadOnlyDictionary<string, ETag> second) =>
        first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var etag) && etag == pair.Value);

    private static async Task<(string Name, ETag ETag)> RequiredAsync(BlobContainerClient master, string name, CancellationToken cancellationToken)
    {
        try
        {
            return (name, (await master.GetBlobClient(name).GetPropertiesAsync(cancellationToken: cancellationToken)).Value.ETag);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException($"The Board's master has no {name}.");
        }
    }

    // The prefix always ends in a slash, so Book cxyz1 never matches cxyz10.
    private static async Task<Dictionary<string, ETag>> ListAsync(BlobContainerClient from, string prefix, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, ETag>(StringComparer.Ordinal);
        await foreach (var blob in from.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
        {
            found[blob.Name] = blob.Properties.ETag ?? default;
        }

        return found;
    }

    // Letters, digits, dot, dash and underscore: a Book id is never a path.
    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex BookId();

    // The prefix comes from the ids alone, and the slash keeps it from matching a longer path.
    public Task RemoveAsync(Guid familyId, Guid childId, CancellationToken cancellationToken) =>
        RemovePrefixAsync(string.Create(CultureInfo.InvariantCulture, $"{familyId:D}/{childId:D}/"), cancellationToken);

    public Task RemoveFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        RemovePrefixAsync(string.Create(CultureInfo.InvariantCulture, $"{familyId:D}/"), cancellationToken);

    private async Task RemovePrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        try
        {
            await Parallel.ForEachAsync(
                container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken),
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDeletes, CancellationToken = cancellationToken },
                async (blob, token) => await container.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: token)
            );
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            // No Workspace was ever started, so there is nothing to remove.
        }
    }
}
