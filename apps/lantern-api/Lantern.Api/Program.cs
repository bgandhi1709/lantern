using System.Text.Json.Serialization;
using Asp.Versioning;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Lantern.Api.Auth;
using Lantern.Api.Configuration;
using Lantern.Api.Exceptions;
using Lantern.Api.Logging;
using Lantern.Api.Mapping;
using Lantern.Api.Services;
using Lantern.Base;
using Lantern.Core.Configuration;
using Lantern.Core.Identity;
using Mapster;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddOptions<FirebaseOptions>()
    .Bind(builder.Configuration.GetSection(FirebaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddLanternBase(builder.Configuration);
// The API hashes uids and sends actions, so it refuses to start without those settings.
builder.Services.AddOptions<SecurityOptions>().ValidateOnStart();
builder.Services.AddOptions<ServiceBusOptions>().ValidateOnStart();
builder.Services.AddOptions<ActionOptions>().ValidateOnStart();
builder
    .Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IRegister, ApiProfile>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IIdentityResolver, HttpIdentityResolver>();
builder.Services.AddSingleton<PendingActionResender>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<PendingActionResender>());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddChildrenPolicy();
});

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

            options.Authority = issuer;
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = projectId,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
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

// Telemetry is off without a connection string, so local runs and the E2E need none. The managed identity signs in; OTEL_SERVICE_NAME is the cloud role name.
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor(options => options.Credential = new DefaultAzureCredential());
    builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(PendingActionResender.ActivitySourceName));
    builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
        options.Filter = context => !context.Request.Path.StartsWithSegments("/health/live", StringComparison.Ordinal)
    );
}

builder.Services.AddHealthChecks();

builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

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
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();

await app.RunAsync();
