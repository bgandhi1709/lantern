using System.Net;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Lantern.Core.Storage;
using Lantern.Repository.Documents;
using Microsoft.Azure.Cosmos;

namespace Lantern.Repository;

internal sealed class ContentRepository(Container content, IJsonService json, ContentLayout layout) : IContentRepository
{
    public async Task<ClassManifest> ClassAsync(BoardType board, int classLevel, CancellationToken cancellationToken)
    {
        var classKey = layout.ClassKey(board, classLevel);
        using var response = await content.ReadItemStreamAsync(layout.ClassDocumentId(classKey), new PartitionKey(classKey), cancellationToken: cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using var reader = new StreamReader(response.Content);
        var manifest =
            json.Deserialize<ClassDocument>(await reader.ReadToEndAsync(cancellationToken))?.Manifest
            ?? throw new InvalidOperationException($"The class document {classKey} has no manifest.");
        manifest.EnsureSchema(layout.ClassManifestSchema);

        return manifest.EnsureChaptersMatchCounts();
    }
}
