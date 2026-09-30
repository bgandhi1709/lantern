using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Azure.Data.Tables;

namespace Lantern.Api.Tests.Infrastructure;

public sealed class AzuriteFixture : IAsyncLifetime, IDisposable
{
    private const string EmulatorAccountKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private readonly StringBuilder output = new();
    private Process? process;
    private string dataDirectory = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var port = GetFreeTcpPort();
        dataDirectory = Directory.CreateTempSubdirectory("lantern-azurite-").FullName;

        ConnectionString = string.Create(
            CultureInfo.InvariantCulture,
            $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
                + $"AccountKey={EmulatorAccountKey};"
                + $"TableEndpoint=http://127.0.0.1:{port}/devstoreaccount1;"
        );

        var startInfo = new ProcessStartInfo("azurite-table")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("--tableHost");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--tablePort");
        startInfo.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--location");
        startInfo.ArgumentList.Add(dataDirectory);
        startInfo.ArgumentList.Add("--silent");

        try
        {
            process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (_, args) => output.AppendLine(args.Data);
            process.ErrorDataReceived += (_, args) => output.AppendLine(args.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start 'azurite-table'. Install it with 'npm install -g azurite'.",
                ex
            );
        }

        await WaitUntilReadyAsync();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (process is { HasExited: false })
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
        }

        process?.Dispose();
        process = null;

        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    public TableServiceClient CreateClient() => new(ConnectionString);

    private async Task WaitUntilReadyAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        var probe = CreateClient().GetTableClient("startupprobe");
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await probe.CreateIfNotExistsAsync();
                return;
            }
            catch (Exception ex) when (DateTimeOffset.UtcNow < deadline)
            {
                lastError = ex;
                await Task.Delay(250);
            }
        }

        throw new TimeoutException(
            "Azurite did not become ready within 20 seconds. "
                + $"Exited: {process?.HasExited}. Output: {output}. "
                + $"Last probe error: {lastError?.Message}"
        );
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
