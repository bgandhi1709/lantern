using Lantern.Api.Tests.Infrastructure;
using Lantern.Base;
using Lantern.Base.Services;
using Lantern.Core.Constants;
using Lantern.Core.Identity;
using Lantern.Core.Models;
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

internal sealed class CallerHolder : IIdentityResolver
{
    public CallerIdentity Identity { get; set; }
}

internal sealed class FamilyCaller : IDisposable
{
    private readonly IServiceScope scope;

    public FamilyCaller(IServiceScope scope, string uid)
    {
        this.scope = scope;
        scope.ServiceProvider.GetRequiredService<CallerHolder>().Identity = new CallerIdentity(uid, "Meena Patel", $"{uid}@example.test", true);
    }

    public IFamilyService Families => scope.ServiceProvider.GetRequiredService<IFamilyService>();

    public IChildService Children => scope.ServiceProvider.GetRequiredService<IChildService>();

    public async Task<Family> RegisterAsync(BoardType board, params int[] classes)
    {
        var registration = new Registration
        {
            FamilyId = Guid.NewGuid(),
            Region = "Gujarat",
            Board = board,
            Language = "gu",
            ParentName = Locked("Meena Patel"),
            ParentEmail = Locked("meena@example.test"),
            RecoveryWrappedKey = Locked("recovery-key"),
            RecoverySalt = Locked("recovery-salt"),
            ConsentAccepted = true,
            ConsentNoticeVersion = "2026-09",
            Children =
            [
                .. classes.Select(classLevel => new Child { ChildId = Guid.NewGuid(), Name = Locked("Child"), ClassLevel = classLevel, BirthYear = Locked("2018"), School = Locked("Sunrise School") }),
            ],
        };

        return await Families.RegisterAsync(registration, CancellationToken.None);
    }

    public async Task EraseFamilyAsync()
    {
        var codes = scope.ServiceProvider.GetRequiredService<IOneTimeCodeService>();
        var challenge = await codes.RequestAsync("delete-family", string.Empty, CancellationToken.None);

        await Families.EraseAsync(codes.Verify(challenge, FixedCodeGenerator.Code), CancellationToken.None);
    }

    public void Dispose() => scope.Dispose();

    private static string Locked(string plaintext) => $"v1.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext))}.{Guid.NewGuid():N}";
}
