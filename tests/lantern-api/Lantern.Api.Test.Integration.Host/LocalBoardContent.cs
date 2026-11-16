using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Azure;
using Azure.Storage.Blobs;
using Lantern.Core.Configuration;
using Microsoft.Azure.Cosmos;

namespace Lantern.Api.Test.Integration.Host;

internal static class LocalBoardContent
{
    private const int HighestClass = 10;
    private static readonly TimeSpan EmulatorStartTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan EmulatorPollInterval = TimeSpan.FromSeconds(1);

    public static async Task LoadAsync(BlobServiceClient blobs, CatalogOptions catalog, CosmosClient cosmos, CosmosOptions options, CancellationToken cancellationToken)
    {
        var content = await CreateContentAsync(cosmos, options, cancellationToken);

        await LoadBoardAsync(blobs.GetBlobContainerClient(catalog.NcertContainer), "cbse", content, cancellationToken);
        await LoadBoardAsync(blobs.GetBlobContainerClient(catalog.SscContainer), "ssc", content, cancellationToken);
    }

    private static async Task<Container> CreateContentAsync(CosmosClient cosmos, CosmosOptions options, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(EmulatorStartTimeout);
        while (true)
        {
            try
            {
                Database database = await cosmos.CreateDatabaseIfNotExistsAsync(options.Database, cancellationToken: cancellationToken);

                return await database.CreateContainerIfNotExistsAsync(options.ContentContainer, "/classKey", cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (DateTimeOffset.UtcNow < deadline && ex is HttpRequestException or CosmosException)
            {
                await Task.Delay(EmulatorPollInterval, cancellationToken);
            }
        }
    }

    private static async Task LoadBoardAsync(BlobContainerClient master, string board, Container content, CancellationToken cancellationToken)
    {
        for (var classLevel = 1; classLevel <= HighestClass; classLevel++)
        {
            var classKey = string.Create(CultureInfo.InvariantCulture, $"{board}-{classLevel}");
            JsonNode manifest;
            try
            {
                var blob = await master.GetBlobClient(string.Create(CultureInfo.InvariantCulture, $"manifest/class-{classLevel}.json")).DownloadContentAsync(cancellationToken);
                manifest = JsonNode.Parse(blob.Value.Content.ToString());
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                continue;
            }

            var document = new JsonObject
            {
                ["id"] = $"class:{classKey}",
                ["kind"] = "class",
                ["classKey"] = classKey,
                ["manifest"] = manifest,
                ["release"] = "local",
            };
            using var body = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
            using var response = await content.UpsertItemStreamAsync(body, new PartitionKey(classKey), cancellationToken: cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }
}
