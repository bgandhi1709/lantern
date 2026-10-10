using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Lantern.Base;
using Lantern.Functions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddLanternBase(builder.Configuration).AddLanternFunctions();

// Telemetry is off without a connection string, so local runs and the E2E need none. OTEL_SERVICE_NAME is the cloud
// role name.
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder
        .Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter(options => options.Credential = new DefaultAzureCredential());
}

await builder.Build().RunAsync();
