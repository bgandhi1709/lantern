using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lantern.Core.Repository;

namespace Lantern.Repository;

// Blob has no empty folders, so a Workspace exists once its marker blob does. createContainer is for Azurite
// only; in Azure the Bicep owns the container.
internal sealed class WorkspaceStore(BlobContainerClient container, bool createContainer) : IWorkspaceStore
{
    private const string MarkerName = "class.json";

    private const int MaxParallelDeletes = 16;

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
