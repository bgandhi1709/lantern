using System.Text.Json.Serialization;
using Asp.Versioning;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Storage.Blobs;
using Lantern.Api.Configuration;
using Lantern.Api.Exceptions;
using Lantern.Api.Logging;
using Lantern.Api.Repository;
using Lantern.Api.Services;
using Lantern.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

LocalDevelopmentGuard.Require(
    builder.Environment,
    builder.Configuration.GetSection(FirebaseOptions.SectionName).Get<FirebaseOptions>() ?? new(),
    builder.Configuration.GetSection(KeyVaultOptions.SectionName).Get<KeyVaultOptions>() ?? new()
);

builder
    .Services.AddOptions<FirebaseOptions>()
    .Bind(builder.Configuration.GetSection(FirebaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<KeyVaultOptions>()
    .Bind(builder.Configuration.GetSection(KeyVaultOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
    var service = string.IsNullOrWhiteSpace(options.ConnectionString)
        ? new TableServiceClient(new Uri(options.TableEndpoint), new DefaultAzureCredential())
        : new TableServiceClient(options.ConnectionString);

    return service;
});

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;

    return string.IsNullOrWhiteSpace(options.ConnectionString)
        ? new BlobServiceClient(new Uri(options.BlobEndpoint), new DefaultAzureCredential())
        : new BlobServiceClient(options.ConnectionString);
});

builder.Services.AddSingleton<IFamilyKeyWrapper>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<KeyVaultOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.LocalKeyPath))
    {
        return new LocalFamilyKeyWrapper(options.LocalKeyPath);
    }

    var keyClient = new KeyClient(new Uri(options.VaultUri), new DefaultAzureCredential());

    return new KeyVaultFamilyKeyWrapper(keyClient, options.FamilyKeyName);
});
builder.Services.AddSingleton<IFamilyKeyService, FamilyKeyService>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<KeyMaterial>();
builder.Services.AddSingleton<IUidHasher>(serviceProvider => serviceProvider.GetRequiredService<KeyMaterial>());
builder.Services.AddSingleton<IFieldCipher, FieldCipher>();
builder.Services.AddSingleton<IFamilyRepository>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
    var service = serviceProvider.GetRequiredService<TableServiceClient>();

    return new FamilyRepository(
        service.GetTableClient(options.ParentsTable),
        service.GetTableClient(options.FamiliesTable),
        createTables: !string.IsNullOrWhiteSpace(options.ConnectionString)
    );
});
builder.Services.AddSingleton<IClassSpaceStore>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
    var service = serviceProvider.GetRequiredService<BlobServiceClient>();

    return new BlobClassSpaceStore(
        service.GetBlobContainerClient(BlobClassSpaceStore.ContainerName),
        createContainer: !string.IsNullOrWhiteSpace(options.ConnectionString)
    );
});
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<DevelopmentSeeder>();

// Google-only sign-in is enforced in the Firebase console. A revoked or disabled user stays valid
// until their ID token expires (1 hour at most).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

builder
    .Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<FirebaseOptions>, ILoggerFactory>(
        (options, firebase, loggerFactory) =>
        {
            var projectId = firebase.Value.ProjectId;
            var issuer = $"https://securetoken.google.com/{projectId}";
            var logger = loggerFactory.CreateLogger("Lantern.Api.Auth");
            var emulator = !string.IsNullOrWhiteSpace(firebase.Value.EmulatorHost);

            // The emulator has no signing keys to fetch; everything else is still checked.
            options.Authority = emulator ? null : issuer;
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = projectId,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = !emulator,
                RequireSignedTokens = !emulator,
                SignatureValidator = emulator ? (token, _) => new JsonWebToken(token) : null,
                ClockSkew = TimeSpan.FromMinutes(2),
            };
            options.Events = new JwtBearerEvents
            {
                // Exception type only: the message can quote token contents.
                OnAuthenticationFailed = context =>
                {
                    Log.TokenInvalid(logger, context.Exception.GetType().Name);
                    return Task.CompletedTask;
                },
            };
        }
    );

// [Authorize] on each controller is the primary gate. This fallback policy is a safety net for any
// future endpoint that forgets one — both require an authenticated caller either way.
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
    );

builder
    .Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new HeaderApiVersionReader("x-api-version");
    })
    .AddMvc();

builder.Services.AddHealthChecks();

builder.Services.AddExceptionHandler<RegistrationExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// `dotnet Lantern.Api.dll seed`: the local Docker seed service. Never in any other environment.
if (args is ["seed"])
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("The seed command is for local development only.");
    }

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync(CancellationToken.None);
    return;
}

// First, so it wraps every other middleware below.
app.UseExceptionHandler();

app.UseForwardedHeaders(
    new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    }
);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();

await app.RunAsync();
