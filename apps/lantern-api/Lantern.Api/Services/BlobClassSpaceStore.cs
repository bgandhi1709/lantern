using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lantern.Api.Services.Interfaces;

namespace Lantern.Api.Services;

// Blob has no empty folders, so a Class space exists once its marker blob does. createContainer is for Azurite
// only; in Azure the Bicep owns the container.
internal sealed class BlobClassSpaceStore(BlobContainerClient container, bool createContainer) : IClassSpaceStore
{
    internal const string ContainerName = "family";
    private const string MarkerName = "class.json";

    public async Task StartAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken)
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

    public async Task DeleteChildAsync(Guid familyId, Guid childId, CancellationToken cancellationToken)
    {
        // The prefix comes from the ids alone, and the slash keeps it from matching a longer path.
        var prefix = string.Create(CultureInfo.InvariantCulture, $"{familyId:D}/{childId:D}/");

        try
        {
            await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
            {
                await container.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: cancellationToken);
            }
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            // No Class space was ever started, so there is nothing to remove.
        }
    }
}
