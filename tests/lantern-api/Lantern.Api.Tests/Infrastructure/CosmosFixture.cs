using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Lantern.Api.Tests.Architecture;
using Microsoft.Azure.Cosmos;

namespace Lantern.Api.Tests.Infrastructure;

public sealed partial class CosmosFixture : IAsyncLifetime, IDisposable
{
    public const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly string name = $"lantern-cosmos-test-{Guid.NewGuid():N}";
    private CosmosClient client;

    public string Endpoint { get; private set; } = string.Empty;

    public Container Content { get; private set; }

    public IReadOnlyDictionary<string, string> Settings =>
        new Dictionary<string, string> { ["Cosmos:Endpoint"] = Endpoint, ["Cosmos:Key"] = EmulatorKey };

    public async ValueTask InitializeAsync()
    {
        var port = GetFreeTcpPort();
        Endpoint = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/");

        await DockerAsync("run", "-d", "--rm", "--name", name, "-e", "PROTOCOL=http", "-p", $"127.0.0.1:{port}:8081", EmulatorImage());

        client = new CosmosClient(Endpoint, EmulatorKey, new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway, LimitToEndpoint = true });
        Content = await CreateContentAsync();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        client?.Dispose();
        client = null;
        using var remove = Process.Start("docker", ["rm", "-f", name]);
        remove?.WaitForExit(10_000);
    }

    private async Task<Container> CreateContentAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);

        while (true)
        {
            try
            {
                Database database = await client.CreateDatabaseIfNotExistsAsync(TestSettings.CosmosDatabase);

                return await database.CreateContainerIfNotExistsAsync(TestSettings.CosmosContentContainer, "/classKey");
            }
            catch (Exception ex) when (DateTimeOffset.UtcNow < deadline && ex is HttpRequestException or CosmosException)
            {
                await Task.Delay(500);
            }
        }
    }

    private static string EmulatorImage()
    {
        var compose = File.ReadAllText(Path.Combine(LanternAssemblies.RepositoryRoot.FullName, "deploy", "local", "docker-compose.yml"));

        return ImageLine().Match(compose) is { Success: true } match
            ? match.Groups["image"].Value
            : throw new InvalidOperationException("deploy/local/docker-compose.yml names no Cosmos DB emulator image.");
    }

    private static async Task DockerAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker", arguments) { RedirectStandardOutput = true, RedirectStandardError = true };

        using var docker = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start 'docker'. The Cosmos DB emulator runs in Docker.");
        var error = await docker.StandardError.ReadToEndAsync();
        await docker.WaitForExitAsync();

        if (docker.ExitCode != 0)
        {
            throw new InvalidOperationException($"docker {arguments[0]} failed: {error}");
        }
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    [GeneratedRegex(@"image:\s*(?<image>mcr\.microsoft\.com/cosmosdb/\S+)")]
    private static partial Regex ImageLine();
}
