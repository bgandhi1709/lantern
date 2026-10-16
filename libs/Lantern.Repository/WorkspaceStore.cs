using System.Net.Mime;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Lantern.Core.Storage;
using MapsterMapper;

namespace Lantern.Repository;

// Blob has no empty folders, so a Workspace exists once its marker blob does. createContainer is for Azurite
// only; in Azure the Bicep owns the container. `boards` holds each Board's master container, which only the Content slice
// (ADR-0013) reads.
internal sealed partial class WorkspaceStore(
    BlobContainerClient container,
    IReadOnlyDictionary<BoardType, BlobContainerClient> boards,
    TimeProvider clock,
    IJsonService json,
    IMapper mapper,
    ContentLayout layout,
    bool createContainer
) : IWorkspaceStore
{
    private const int MaxParallelDeletes = 16;
    private const int MaxParallelCopies = 16;
    private const int MaxManifestAttempts = 12;
    private const int MaxCopiesInFlight = 32;
    private static readonly SemaphoreSlim InFlight = new(MaxCopiesInFlight, MaxCopiesInFlight);
    private Dictionary<BoardType, BoardRules> Rules { get; } = new()
    {
        [BoardType.Cbse] = new BoardRules(skipsUnbuiltBooks: true, boardFiles: [], bookFiles: []),
        [BoardType.Ssc] = new BoardRules(skipsUnbuiltBooks: false, boardFiles: [layout.BoardProfileFile], bookFiles: [layout.TocFolder, layout.GuidanceFolder]),
    };

    private sealed class BoardRules(bool skipsUnbuiltBooks, string[] boardFiles, string[] bookFiles)
    {
        public bool SkipsUnbuiltBooks { get; } = skipsUnbuiltBooks;

        public string[] BoardFiles { get; } = boardFiles;

        public string[] BookFiles { get; } = bookFiles;
    }

    private sealed class BookSnapshot(string book, JsonNode entry, Dictionary<string, ETag> files)
    {
        public string Book { get; } = book;

        public JsonNode Entry { get; } = entry;

        public Dictionary<string, ETag> Files { get; } = files;
    }

    private sealed class ContentSnapshot(
        IReadOnlyList<string> files,
        byte[] catalogBytes,
        byte[] classManifestBytes,
        IReadOnlyList<string> books,
        IReadOnlyDictionary<string, ETag> etags
    )
    {
        public IReadOnlyList<string> Files { get; } = files;

        public byte[] CatalogBytes { get; } = catalogBytes;

        public byte[] ClassManifestBytes { get; } = classManifestBytes;

        public IReadOnlyList<string> Books { get; } = books;

        public IReadOnlyDictionary<string, ETag> Etags { get; } = etags;
    }

    public async Task CreateAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken)
    {
        if (createContainer)
        {
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        var path = layout.MarkerPath(familyId, childId, classLevel);
        var options = new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } };

        try
        {
            await container.GetBlobClient(path).UploadAsync(BinaryData.Empty, options, cancellationToken);
        }
        catch (RequestFailedException ex) when (IsAlreadyThere(ex))
        {
        }
    }

    public async Task CopyContentAsync(Guid familyId, Guid childId, int classLevel, BoardType board, CancellationToken cancellationToken)
    {
        var prefix = layout.ContentFolder(familyId, childId, classLevel);
        var version = container.GetBlobClient(prefix + layout.VersionFile);

        if (await version.ExistsAsync(cancellationToken))
        {
            return;
        }

        var master = boards.TryGetValue(board, out var found)
            ? found
            : throw new InvalidOperationException($"No master container is set for the {board} Board.");
        var snapshot = await ReadSnapshotAsync(master, Rules[board], classLevel, cancellationToken);

        await CopyFilesAsync(master, snapshot, prefix, cancellationToken);
        await RemoveLeftoversAsync(snapshot, prefix, cancellationToken);

        var secondSnapshot = await ReadSnapshotAsync(master, Rules[board], classLevel, cancellationToken);
        if (!SameSnapshot(snapshot.Etags, secondSnapshot.Etags))
        {
            throw new InvalidOperationException($"The {board} Board's master changed while Class {classLevel} was being copied.");
        }

        // Last, so a half copy is never ready.
        var made = new ContentVersion
        {
            Board = board.ToString().ToLowerInvariant(),
            Class = classLevel,
            Books = [.. snapshot.Books],
            Files = snapshot.Files.Count + 2,
            CopiedAt = clock.GetUtcNow(),
        };
        await version.UploadAsync(BinaryData.FromString(json.SerializeIndented(made)), overwrite: true, cancellationToken);
    }

    public async Task<IReadOnlyList<Subject>> SubjectsAsync(Guid familyId, Guid childId, CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await container.GetBlobClient(layout.FamilyManifestPath(familyId)).DownloadContentAsync(cancellationToken);

            return ParseFamilyManifest(manifest.Value.Content.ToString()).SubjectsOf(childId);
        }
        catch (RequestFailedException ex) when (IsMissing(ex))
        {
            return null;
        }
    }

    public async Task AddToManifestAsync(Guid familyId, Guid childId, int classLevel, BoardType board, CancellationToken cancellationToken)
    {
        var slice = layout.ContentFolder(familyId, childId, classLevel);
        if (!await container.GetBlobClient(slice + layout.VersionFile).ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException("The Child's Content slice is not ready, so the Child has no place in the Family manifest yet.");
        }

        var classManifest = ParseClassManifest((await container.GetBlobClient(slice + layout.ClassManifestFile).DownloadContentAsync(cancellationToken)).Value.Content.ToString());
        var entry = mapper.Map<FamilyChild>(classManifest);
        var whenMissing = FamilyManifest.Empty(layout.FamilyManifestSchema, board.ToString().ToLowerInvariant());

        await UpdateManifestAsync(
            familyId,
            whenMissing,
            manifest =>
            {
                if (manifest.Has(childId, entry))
                {
                    return false;
                }

                manifest.Add(childId, entry);

                return true;
            },
            cancellationToken
        );
    }

    private Task RemoveFromManifestAsync(Guid familyId, Guid childId, CancellationToken cancellationToken)
    {
        var nothingToRemove = FamilyManifest.Empty(layout.FamilyManifestSchema, board: string.Empty);

        return UpdateManifestAsync(familyId, nothingToRemove, manifest => manifest.Remove(childId), cancellationToken);
    }

    private async Task UpdateManifestAsync(Guid familyId, FamilyManifest whenMissing, Func<FamilyManifest, bool> change, CancellationToken cancellationToken)
    {
        if (createContainer)
        {
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        var blob = container.GetBlobClient(layout.FamilyManifestPath(familyId));
        for (var attempt = 0; attempt < MaxManifestAttempts; attempt++)
        {
            var (stored, etag) = await DownloadFamilyManifestAsync(blob, cancellationToken);
            var manifest = stored ?? whenMissing;
            if (!change(manifest))
            {
                return;
            }

            var conditions = etag is { } found ? new BlobRequestConditions { IfMatch = found } : new BlobRequestConditions { IfNoneMatch = ETag.All };
            var options = new BlobUploadOptions { Conditions = conditions, HttpHeaders = new BlobHttpHeaders { ContentType = MediaTypeNames.Application.Json } };
            try
            {
                await blob.UploadAsync(BinaryData.FromString(json.Serialize(manifest)), options, cancellationToken);

                return;
            }
            catch (RequestFailedException ex) when (IsLostRace(ex))
            {
            }
        }

        throw new InvalidOperationException($"The Family manifest kept changing; gave up after {MaxManifestAttempts} tries.");
    }

    private static bool IsLostRace(RequestFailedException ex) => ex.Status == 412 || IsAlreadyThere(ex);

    private static bool IsAlreadyThere(RequestFailedException ex) => ex.ErrorCode == BlobErrorCode.BlobAlreadyExists;

    private static bool IsMissing(RequestFailedException ex) => ex.Status == 404;

    private static bool IsContainerMissing(RequestFailedException ex) => ex.ErrorCode == BlobErrorCode.ContainerNotFound;

    private async Task<(FamilyManifest Manifest, ETag? ETag)> DownloadFamilyManifestAsync(BlobClient blob, CancellationToken cancellationToken)
    {
        try
        {
            var read = (await blob.DownloadContentAsync(cancellationToken)).Value;

            return (ParseFamilyManifest(read.Content.ToString()), read.Details.ETag);
        }
        catch (RequestFailedException ex) when (IsMissing(ex))
        {
            return (null, null);
        }
    }

    private FamilyManifest ParseFamilyManifest(string text) => Parse<FamilyManifest>(text, layout.FamilyManifestSchema);

    private ClassManifest ParseClassManifest(string text) => Parse<ClassManifest>(text, layout.ClassManifestSchema).EnsureChaptersMatchCounts();

    private T Parse<T>(string text, string schema)
        where T : Manifest
    {
        var manifest = json.Deserialize<T>(text) ?? throw new InvalidOperationException($"The {typeof(T).Name} is empty.");
        manifest.EnsureSchema(schema);

        return manifest;
    }

    private async Task CopyFilesAsync(BlobContainerClient master, ContentSnapshot snapshot, string prefix, CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(
            snapshot.Files,
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
        await container.GetBlobClient(prefix + layout.CatalogFile).UploadAsync(BinaryData.FromBytes(snapshot.CatalogBytes), overwrite: true, cancellationToken);
        await container.GetBlobClient(prefix + layout.ClassManifestFile).UploadAsync(BinaryData.FromBytes(snapshot.ClassManifestBytes), overwrite: true, cancellationToken);
    }

    // version.json is never touched: a copy running beside this one may have just written it.
    private async Task RemoveLeftoversAsync(ContentSnapshot snapshot, string prefix, CancellationToken cancellationToken)
    {
        var wanted = new HashSet<string>([.. snapshot.Files, layout.CatalogFile, layout.ClassManifestFile, layout.VersionFile], StringComparer.Ordinal);
        var leftovers = (await ListAsync(container, prefix, cancellationToken)).Keys.Where(name => !wanted.Contains(name[prefix.Length..]));

        await Parallel.ForEachAsync(
            leftovers,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDeletes, CancellationToken = cancellationToken },
            async (name, token) => await container.DeleteBlobIfExistsAsync(name, cancellationToken: token)
        );
    }

    // Everything is checked before the first write, so a missing file leaves nothing behind.
    private async Task<ContentSnapshot> ReadSnapshotAsync(BlobContainerClient master, BoardRules rules, int classLevel, CancellationToken cancellationToken)
    {
        var catalogRead = (await master.GetBlobClient(layout.CatalogFile).DownloadContentAsync(cancellationToken)).Value;
        var catalog = json.Deserialize<JsonArray>(catalogRead.Content.ToString()) ?? throw new InvalidOperationException("The Board's catalog is not a list of Books.");

        // A Class has at most a handful of Books, so they are read together.
        var entries = catalog.OfType<JsonObject>().Where(entry => IsGrade(entry, classLevel));
        var books = (await Task.WhenAll(entries.Select(entry => ReadBookSnapshotAsync(master, rules, entry, classLevel, cancellationToken)))).Where(book => book is not null).ToList();
        if (books.Count == 0)
        {
            throw new InvalidOperationException($"The Board has no built Book for Class {classLevel}.");
        }

        var (classManifestName, classManifest, classManifestEtag) = await DownloadMasterClassManifestAsync(master, classLevel, cancellationToken);
        var etags = new Dictionary<string, ETag>(StringComparer.Ordinal) { [layout.CatalogFile] = catalogRead.Details.ETag, [classManifestName] = classManifestEtag };
        foreach (var (name, etag) in books.SelectMany(book => book.Files))
        {
            etags[name] = etag;
        }

        foreach (var (name, etag) in await Task.WhenAll(rules.BoardFiles.Select(name => RequiredAsync(master, name, cancellationToken))))
        {
            etags[name] = etag;
        }

        var kept = new JsonArray([.. books.Select(book => book.Entry.DeepClone())]);
        string[] files = [.. etags.Keys.Where(name => name != layout.CatalogFile && name != classManifestName).Order(StringComparer.Ordinal)];

        return new ContentSnapshot(files, Encoding.UTF8.GetBytes(json.SerializeIndented(kept)), classManifest, [.. books.Select(book => book.Book)], etags);
    }

    private async Task<(string Name, byte[] Bytes, ETag ETag)> DownloadMasterClassManifestAsync(BlobContainerClient master, int classLevel, CancellationToken cancellationToken)
    {
        var name = layout.MasterClassManifest(classLevel);
        try
        {
            var read = (await master.GetBlobClient(name).DownloadContentAsync(cancellationToken)).Value;
            if (ParseClassManifest(read.Content.ToString()).Class != classLevel)
            {
                throw new InvalidOperationException($"The Board's {name} is for another Class.");
            }

            return (name, read.Content.ToArray(), read.Details.ETag);
        }
        catch (RequestFailedException ex) when (IsMissing(ex))
        {
            throw new InvalidOperationException($"The Board's master has no {name}.");
        }
    }

    // Null for a Book NCERT lists and has not built; for SSC an unbuilt Book, or a missing file, is a broken master.
    private async Task<BookSnapshot> ReadBookSnapshotAsync(
        BlobContainerClient master,
        BoardRules rules,
        JsonObject entry,
        int classLevel,
        CancellationToken cancellationToken
    )
    {
        var book = entry[layout.CatalogBookField]?.GetValue<string>();
        if (book is null || !BookId().IsMatch(book))
        {
            throw new InvalidOperationException("A catalog entry has no usable book_id.");
        }

        var refinedTask = ListAsync(master, layout.RefinedFolder(book), cancellationToken);
        var summariesTask = ListAsync(master, layout.SummariesFolder(book), cancellationToken);
        var (refined, summaries) = (await refinedTask, await summariesTask);

        if (refined.Count == 0)
        {
            return rules.SkipsUnbuiltBooks
                ? null
                : throw new InvalidOperationException($"The Board lists Book {book} for Class {classLevel} but has no refined content for it.");
        }

        var unsummarised = refined.Keys.FirstOrDefault(chapter => !summaries.ContainsKey(layout.SummaryOf(chapter)));
        if (unsummarised is not null)
        {
            throw new InvalidOperationException($"Book {book} has refined content with no summary: {unsummarised}.");
        }

        var files = new Dictionary<string, ETag>(refined.Concat(summaries), StringComparer.Ordinal);
        foreach (var (name, etag) in await Task.WhenAll(rules.BookFiles.Select(folder => RequiredAsync(master, layout.BookFile(folder, book), cancellationToken))))
        {
            files[name] = etag;
        }

        return new BookSnapshot(book, entry, files);
    }

    private bool IsGrade(JsonObject entry, int classLevel) =>
        entry[layout.CatalogGradeField] is JsonValue grade && grade.TryGetValue<int>(out var value) && value == classLevel;

    private static bool SameSnapshot(IReadOnlyDictionary<string, ETag> first, IReadOnlyDictionary<string, ETag> second) =>
        first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var etag) && etag == pair.Value);

    private static async Task<(string Name, ETag ETag)> RequiredAsync(BlobContainerClient master, string name, CancellationToken cancellationToken)
    {
        try
        {
            return (name, (await master.GetBlobClient(name).GetPropertiesAsync(cancellationToken: cancellationToken)).Value.ETag);
        }
        catch (RequestFailedException ex) when (IsMissing(ex))
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
        Task.WhenAll(
            RemovePrefixAsync(layout.ChildFolder(familyId, childId), cancellationToken),
            RemoveFromManifestAsync(familyId, childId, cancellationToken)
        );

    public Task RemoveFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        RemovePrefixAsync(layout.FamilyFolder(familyId), cancellationToken);

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
        catch (RequestFailedException ex) when (IsContainerMissing(ex))
        {
        }
    }
}
