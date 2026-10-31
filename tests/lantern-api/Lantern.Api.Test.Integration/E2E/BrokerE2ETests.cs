using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Azure.Storage.Blobs;
using Lantern.Api.Models;

namespace Lantern.Api.Test.Integration.E2E;

[Trait("Category", "E2E")]
public sealed class BrokerE2ETests(RegisteredFamilyFixture fixture) : IClassFixture<RegisteredFamilyFixture>
{
    private const string Exchange = "lantern.actions";
    private const string AzuriteBlobs =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
        + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
        + "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    [Fact]
    public async Task AKeyWithNoBinding_LandsInTheParkingQueue()
    {
        using var broker = Broker();
        var marker = $"unroutable-{Guid.NewGuid():N}";

        var published = await broker.PostAsync(
            $"exchanges/%2F/{Exchange}/publish",
            Json(new { routing_key = "no-such-action", payload = marker, payload_encoding = "string", properties = new { } })
        );
        published.EnsureSuccessStatusCode();

        var parked = new List<string>();
        Assert.True(await WaitAsync(async () => HasSeen(parked, await TakeAsync(broker, "parking"), marker)), "The message was not parked.");
    }

    [Fact]
    public async Task ASecondQueueBoundToTheKey_ReceivesTheSameActionAsTheSubscriber()
    {
        using var broker = Broker();
        var second = $"e2e-second-{Guid.NewGuid():N}";
        (await broker.PutAsync($"queues/%2F/{second}", Json(new { durable = false, auto_delete = false, arguments = new { } }))).EnsureSuccessStatusCode();
        (await broker.PostAsync($"bindings/%2F/e/{Exchange}/q/{second}", Json(new { routing_key = "remove-workspace" }))).EnsureSuccessStatusCode();

        try
        {
            var id = Guid.NewGuid();
            var added = await fixture.Client.PostAsJsonAsync(
                new Uri("/v1/family/children", UriKind.Relative),
                new ChildAddModel { ChildId = id, Name = "locked-fan", ClassLevel = 6, BirthYear = "locked-year", School = "locked-school" }
            );
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
            var marker = new BlobServiceClient(AzuriteBlobs)
                .GetBlobContainerClient("family")
                .GetBlobClient($"{fixture.Family.FamilyId:D}/{id:D}/6/class.json");
            Assert.True(await WaitAsync(async () => await marker.ExistsAsync()), "The Workspace was not started.");

            var deleted = await fixture.Client.DeleteAsync(new Uri($"/v1/family/children/{id}", UriKind.Relative));
            Assert.Equal(HttpStatusCode.Accepted, deleted.StatusCode);

            var received = new List<string>();
            Assert.True(
                await WaitAsync(async () => HasSeen(received, await TakeAsync(broker, second), id.ToString("D"))),
                "The second queue did not receive the action."
            );
            Assert.True(await WaitAsync(async () => !await marker.ExistsAsync()), "The subscriber did not remove the Workspace.");
        }
        finally
        {
            await broker.DeleteAsync($"queues/%2F/{second}");
        }
    }

    private static JsonContent Json(object body) => JsonContent.Create(body, new MediaTypeHeaderValue("application/json"));

    private static HttpClient Broker()
    {
        var api = Environment.GetEnvironmentVariable("E2E_BROKER_API") ?? "http://127.0.0.1:15672/api/";
        var client = new HttpClient { BaseAddress = new Uri(api) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));

        return client;
    }

    private static async Task<List<string>> TakeAsync(HttpClient broker, string queue)
    {
        var response = await broker.PostAsync($"queues/%2F/{queue}/get", Json(new { count = 50, ackmode = "ack_requeue_false", encoding = "auto" }));
        response.EnsureSuccessStatusCode();
        var messages = await response.Content.ReadFromJsonAsync<List<BrokerMessage>>() ?? [];

        return [.. messages.Select(message => message.Payload)];
    }

    private static bool HasSeen(List<string> seen, List<string> taken, string text)
    {
        seen.AddRange(taken);

        return seen.Any(body => body.Contains(text, StringComparison.Ordinal));
    }

    private static async Task<bool> WaitAsync(Func<Task<bool>> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (await done())
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }
}
