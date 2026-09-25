using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lantern.Api.Tests.Infrastructure;

/// <summary>
/// Boots the API in-process for integration tests, pointed at a real Azurite table endpoint.
/// </summary>
/// <remarks>
/// Nothing reads <c>Storage:ConnectionString</c> yet; a later task wires storage into the app and
/// starts consuming it. It is set here now so every test class shares the same factory shape.
/// </remarks>
public sealed class LanternApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Storage:ConnectionString", connectionString);
    }
}
