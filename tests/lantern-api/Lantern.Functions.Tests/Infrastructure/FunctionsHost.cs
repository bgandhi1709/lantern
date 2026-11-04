using Lantern.Api.Tests.Infrastructure;
using Lantern.Base;
using Lantern.Core.Identity;
using Lantern.Core.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lantern.Functions.Tests.Infrastructure;

internal sealed class FunctionsHost : IDisposable
{
    private readonly ServiceProvider provider;
    private int delivered;

    public FunctionsHost(string connectionString, IReadOnlyDictionary<string, string> settings = null, Action<IServiceCollection> configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(TestSettings.FilePath, optional: false)
            .AddInMemoryCollection(new Dictionary<string, string> { ["Storage:ConnectionString"] = connectionString })
            .AddInMemoryCollection(settings ?? new Dictionary<string, string>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(Logs));
        services.AddSingleton<IPublishingService>(Sender);
        services.AddSingleton<IEmailService>(Emails);
        services.AddSingleton<IOneTimeCodeGenerator>(Codes);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<CallerHolder>();
        services.AddScoped<IIdentityResolver>(scope => scope.GetRequiredService<CallerHolder>());
        services.AddLanternBase(configuration).AddLanternFunctions();
        configure?.Invoke(services);
        provider = services.BuildServiceProvider();
    }

    public CapturingLoggerProvider Logs { get; } = new();

    public RecordingPublishingService Sender { get; } = new();

    public RecordingEmailService Emails { get; } = new();

    public FixedCodeGenerator Codes { get; } = new();

    public TestClock Clock { get; } = new();

    public IServiceProvider Services => provider;

    public FamilyCaller Caller(string uid) => new(provider.CreateScope(), uid);

    public async Task DeliverAsync()
    {
        var all = Sender.Sent;

        while (delivered < all.Count)
        {
            await Services.DeliverAsync(all[delivered++]);
        }
    }

    public void Dispose() => provider.Dispose();
}
