using Lantern.Api.Tests.Infrastructure;
using Lantern.Base.Services;
using Lantern.Core.Constants;
using Lantern.Core.Identity;
using Lantern.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Functions.Tests.Infrastructure;

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
