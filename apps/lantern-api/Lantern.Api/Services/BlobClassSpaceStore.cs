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

    private volatile bool _containerEnsured;

    public async Task StartAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);

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

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (!createContainer || _containerEnsured)
        {
            return;
        }

        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _containerEnsured = true;
    }
}
