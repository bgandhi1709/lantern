using Lantern.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Lantern.Api.Test.Integration.Host;

// The API with local stand-ins: a key file for Key Vault, the Firebase Auth Emulator's unsigned tokens, and the dev Family.
// ConfigureTestServices runs after the API's own registrations, which is what lets these replace them.
internal sealed class LocalApiFactory(string keyPath, string contentRoot) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseContentRoot(contentRoot);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IFamilyKeyWrapper>(new LocalFamilyKeyWrapper(keyPath));
            services.AddScoped<DevelopmentSeeder>();
            services.AddHostedService<SeedOnStartup>();
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, AcceptEmulatorTokens);
        });
    }

    // The emulator has no signing keys to fetch; issuer, audience and expiry are still checked.
    private static void AcceptEmulatorTokens(JwtBearerOptions options)
    {
        options.Authority = null;
        options.ConfigurationManager = null;
        options.TokenValidationParameters.RequireSignedTokens = false;
        options.TokenValidationParameters.ValidateIssuerSigningKey = false;
        options.TokenValidationParameters.SignatureValidator = (token, _) => new JsonWebToken(token);
    }
}

internal sealed class SeedOnStartup(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
