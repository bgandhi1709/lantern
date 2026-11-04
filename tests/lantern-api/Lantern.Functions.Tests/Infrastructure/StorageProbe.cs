using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Repository;

namespace Lantern.Functions.Tests.Infrastructure;

internal sealed class StorageProbe(AzuriteFixture azurite)
{
    public RowKeyService Keys { get; } = new();

    public BlobContainerClient Blobs => azurite.CreateBlobClient().GetBlobContainerClient(TestSettings.WorkspaceContainer);

    public TableClient Families => azurite.CreateClient().GetTableClient("families");

    public TableClient Parents => azurite.CreateClient().GetTableClient("parents");

    public TableClient Actions => azurite.CreateClient().GetTableClient("actions");

    public async Task<List<string>> BlobNamesAsync(string prefix)
    {
        var names = new List<string>();
        try
        {
            await foreach (var blob in Blobs.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
            {
                names.Add(blob.Name);
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
        }

        return names;
    }

    public async Task<List<TableEntity>> LedgerAsync(ActionType type, string rowKey) =>
        await Actions.QueryAsync<TableEntity>(row => row.PartitionKey == type.ToString() && row.RowKey == rowKey).ToListAsync();

    public async Task<List<TableEntity>> RemoveWorkspaceLedgerAsync(Guid familyId) =>
        await Actions
            .QueryAsync<TableEntity>(row => row.PartitionKey == nameof(ActionType.RemoveWorkspace) && row.RowKey.CompareTo($"{familyId:N}_") >= 0 && row.RowKey.CompareTo($"{familyId:N}`") < 0)
            .ToListAsync();

    public async Task<List<TableEntity>> FamilyRowsAsync(Guid familyId) =>
        await Families.QueryAsync<TableEntity>(row => row.PartitionKey == familyId.ToString()).ToListAsync();

    public async Task<List<TableEntity>> ProfilesAsync(Guid familyId) =>
        await Parents.QueryAsync<TableEntity>(row => row.GetGuid("FamilyId") == familyId).ToListAsync();
}
