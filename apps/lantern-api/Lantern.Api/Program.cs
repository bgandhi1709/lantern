using System.Text.Json.Serialization;
using Asp.Versioning;
using Azure.Data.Tables;
using Azure.Identity;
using Lantern.Api.Auth;
using Lantern.Api.Configuration;
using Lantern.Api.Middleware;
using Lantern.Api.Repository;
using Lantern.Api.Services;
using Lantern.Api.Services.Interfaces;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
    var service = string.IsNullOrWhiteSpace(options.ConnectionString)
        ? new TableServiceClient(new Uri(options.TableEndpoint), new DefaultAzureCredential())
        : new TableServiceClient(options.ConnectionString);

    return service.GetTableClient(options.ParentsTable);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<KeyMaterial>();
builder.Services.AddSingleton<IUidHasher>(serviceProvider => serviceProvider.GetRequiredService<KeyMaterial>());
builder.Services.AddSingleton<IFieldCipher, FieldCipher>();
builder.Services.AddSingleton<IParentRepository, TableParentRepository>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

builder.Services.AddFirebaseAuthentication();

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

var app = builder.Build();

app.UseForwardedHeaders(
    new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    }
);

app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();

await app.RunAsync();
