using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;

namespace Lantern.Api.Test.Integration.Host;

// Local Docker only (deploy/local/api.Dockerfile). Settings come from the environment, as for the API itself.
internal static class LocalHost
{
    private static void Main()
    {
        var certificate = X509Certificate2.CreateFromPemFile(
            Environment.GetEnvironmentVariable("Kestrel__Certificates__Default__Path") ?? "/certs/local.lantern.api.crt",
            Environment.GetEnvironmentVariable("Kestrel__Certificates__Default__KeyPath") ?? "/certs/local.lantern.api.key"
        );

        using var factory = new LocalApiFactory(AppContext.BaseDirectory);
        factory.UseKestrel(options => options.ListenAnyIP(8443, listen => listen.UseHttps(certificate)));
        factory.StartServer();

        using var shutdown = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            shutdown.Set();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Set();
        shutdown.Wait();
    }
}
