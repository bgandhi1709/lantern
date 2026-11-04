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
public sealed class LanternApiFactory(string connectionString, IReadOnlyDictionary<string, string> settings = null) : WebApplicationFactory<Program>
{
    public CapturingLoggerProvider Logs { get; } = new();

    public RecordingPublishingService Sender { get; } = new();

    public RecordingEmailService Emails { get; } = new();

    public FixedCodeGenerator Codes { get; } = new();

    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Firebase:ProjectId", TestTokens.ProjectId);
        builder.UseSetting("Storage:ConnectionString", connectionString);
        // The fixed values are in appsettings.Testing.json; only what the test run decides is set here.
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddJsonFile(TestSettings.FilePath, optional: false);
            config.AddInMemoryCollection(settings ?? new Dictionary<string, string>());
        });

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureTestServices(services =>
        {
            // The start-up resend is run by the tests that cover it.
            services.RemoveAll<IHostedService>();
            services.AddSingleton<IPublishingService>(Sender);
            services.AddSingleton<IEmailService>(Emails);
            services.AddSingleton<IOneTimeCodeGenerator>(Codes);
            services.AddSingleton<TimeProvider>(Clock);
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
