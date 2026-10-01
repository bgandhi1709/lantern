using Lantern.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Lantern.Api.Tests.Infrastructure;

// Only the signing-key source is replaced; issuer, audience, lifetime and signature checks are the production settings.
public sealed class LanternApiFactory(string connectionString, bool runDeletionWorker = false)
    : WebApplicationFactory<Program>
{
    public const string SecurityKey = "dGVzdC1rZXktb25seS1mb3ItdGhlLXRlc3Qtc3VpdGUtMDE=";

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Firebase:ProjectId", TestTokens.ProjectId);
        builder.UseSetting("Storage:ConnectionString", connectionString);
        builder.UseSetting("Security:Key", SecurityKey);
        // Key Vault has no local emulator; LocalRsaFamilyKeyWrapper below replaces the real wrapper,
        // so these two values only need to satisfy startup validation, never actually resolve.
        builder.UseSetting("KeyVault:VaultUri", "https://unused.vault.azure.net/");
        builder.UseSetting("KeyVault:FamilyKeyName", "unused");

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.UseSetting("Deletion:PollSeconds", "1");

        builder.ConfigureTestServices(services =>
        {
            // Tests drive the deletion worker themselves unless they ask for the real loop.
            if (!runDeletionWorker)
            {
                services.RemoveAll<IHostedService>();
            }

            services.AddSingleton<IFamilyKeyWrapper, LocalRsaFamilyKeyWrapper>();
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
