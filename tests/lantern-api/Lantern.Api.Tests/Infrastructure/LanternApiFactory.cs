extern alias Functions;

using Functions::Lantern.Functions;
using Lantern.Core.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Lantern.Api.Tests.Infrastructure;

// Only the signing-key source is replaced; issuer, audience, lifetime and signature checks are the production settings.
public sealed class LanternApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    private int delivered;

    public CapturingLoggerProvider Logs { get; } = new();

    public RecordingPublishingService Sender { get; } = new();

    /// <summary>Hands every message published so far, and not yet delivered, to the function bound to its key, as the broker does.</summary>
    public async Task DeliverAsync()
    {
        var all = Sender.Sent;

        while (delivered < all.Count)
        {
            await Services.DeliverAsync(all[delivered++]);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Firebase:ProjectId", TestTokens.ProjectId);
        builder.UseSetting("Storage:ConnectionString", connectionString);
        // The fixed values are in appsettings.Testing.json; only what the test run decides is set here.
        builder.ConfigureAppConfiguration(config => config.AddJsonFile(TestSettings.FilePath, optional: false));

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));


        builder.ConfigureTestServices(services =>
        {
            // The start-up resend is run by the tests that cover it.
            services.RemoveAll<IHostedService>();
            services.AddSingleton<IPublishingService>(Sender);
            // The Functions app's handlers, so a test can deliver what the API sent.
            services.AddLanternFunctions();
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            new OpenIdConnectConfiguration
                            {
                                Issuer = $"https://securetoken.google.com/{TestTokens.ProjectId}",
                                SigningKeys = { TestTokens.Key },
                            }
                        )
            );
        });
    }
}
