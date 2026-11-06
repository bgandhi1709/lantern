using System.Text.Json;
using Azure;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Functions.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lantern.Functions.Tests;

[Collection(FunctionsCollection.Name)]
public sealed class FamilyActionTests(AzuriteFixture azurite) : IDisposable
{
    private readonly FunctionsHost host = new(azurite.ConnectionString);

    private readonly StorageProbe storage = new(azurite);

    private IReadOnlyList<ActionMessage> Erasures => [.. host.Sender.Sent.Where(m => m.Type == ActionType.RemoveFamily)];

    [Fact]
    public async Task Register_StartsAWorkspaceForEachChildAtTheirClass()
    {
        using var caller = host.Caller(NewUid());

        var family = await RegisteredAsync(caller);

        foreach (var child in family.Children)
        {
            Assert.True(await storage.Blobs.GetBlobClient($"{family.FamilyId:D}/{child.ChildId:D}/{child.ClassLevel}/class.json").ExistsAsync());
        }
    }

    [Fact]
    public async Task Erase_LocksTheCallerOutAtOnce_AndTheFunctionErasesEverythingOfItAndNothingElse()
    {
        using var caller = host.Caller(NewUid());
        using var other = host.Caller(NewUid());
        var mine = await RegisteredAsync(caller);
        var theirs = await RegisteredAsync(other);

        await caller.EraseFamilyAsync();

        Assert.Empty(await storage.ProfilesAsync(mine.FamilyId));
        Assert.Single(Erasures);
        Assert.Single(await storage.PendingLedgerAsync(ActionType.RemoveFamily, mine.FamilyId.ToString("N")));

        await host.DeliverAsync();

        Assert.Empty(await storage.FamilyRowsAsync(mine.FamilyId));
        Assert.Empty(await storage.BlobNamesAsync($"{mine.FamilyId:D}/"));
        Assert.Empty(await storage.PendingLedgerAsync(ActionType.RemoveFamily, mine.FamilyId.ToString("N")));
        Assert.Equal("remove-family", await storage.DoneByAsync(ActionType.RemoveFamily, mine.FamilyId.ToString("N")));
        Assert.Equal(2, (await other.Families.MeAsync(CancellationToken.None)).Children.Count);
        Assert.NotEmpty(await storage.BlobNamesAsync($"{theirs.FamilyId:D}/"));
    }

    [Fact]
    public async Task Erase_TheSameMessageDeliveredAtOnce_EndsAsIfDeliveredOnce_AndLeavesAnotherFamilyAlone()
    {
        using var caller = host.Caller(NewUid());
        using var other = host.Caller(NewUid());
        var mine = await RegisteredAsync(caller);
        var theirs = await RegisteredAsync(other);
        await caller.EraseFamilyAsync();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => host.Services.DeliverAsync(Erasures.Single())));

        Assert.Empty(await storage.FamilyRowsAsync(mine.FamilyId));
        Assert.Empty(await storage.BlobNamesAsync($"{mine.FamilyId:D}/"));
        Assert.Empty(await storage.PendingLedgerAsync(ActionType.RemoveFamily, mine.FamilyId.ToString("N")));
        Assert.Equal(2, (await other.Families.MeAsync(CancellationToken.None)).Children.Count);
        Assert.NotEmpty(await storage.BlobNamesAsync($"{theirs.FamilyId:D}/"));
    }

    [Fact]
    public async Task Erase_ThenRegisterBeforeTheHandlerRuns_StartsANewFamily_ThatTheErasureLeavesAlone()
    {
        var uid = NewUid();
        using var caller = host.Caller(uid);
        var old = await RegisteredAsync(caller);

        await caller.EraseFamilyAsync();
        var now = await caller.RegisterAsync(BoardType.Cbse, 5, 3);
        await host.DeliverAsync();

        Assert.NotEqual(old.FamilyId, now.FamilyId);
        Assert.Equal(now.FamilyId, (await caller.Families.MeAsync(CancellationToken.None)).FamilyId);
        Assert.Empty(await storage.FamilyRowsAsync(old.FamilyId));
    }

    [Fact]
    public async Task WhenTheBlobStepFails_KeepsTheLedger_AndFinishesWhenTheMessageIsDeliveredAgain()
    {
        using var caller = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        await caller.EraseFamilyAsync();
        var failing = new Mock<IWorkspaceStore>();
        failing
            .Setup(s => s.RemoveFamilyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "blob down"));
        using var broken = new FunctionsHost(azurite.ConnectionString, configure: services => services.AddSingleton(failing.Object));

        await Assert.ThrowsAsync<RequestFailedException>(() => broken.Services.DeliverAsync(Erasures[0]));

        Assert.Single(await storage.PendingLedgerAsync(ActionType.RemoveFamily, family.FamilyId.ToString("N")));
        Assert.NotEmpty(await storage.BlobNamesAsync($"{family.FamilyId:D}/"));

        await host.DeliverAsync();

        Assert.Empty(await storage.PendingLedgerAsync(ActionType.RemoveFamily, family.FamilyId.ToString("N")));
        Assert.Empty(await storage.BlobNamesAsync($"{family.FamilyId:D}/"));
        Assert.Empty(await storage.FamilyRowsAsync(family.FamilyId));
    }

    [Fact]
    public async Task TheSameMessageTwiceAtOnce_FinishesWithoutError()
    {
        using var caller = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        await caller.EraseFamilyAsync();

        await Task.WhenAll(host.Services.DeliverAsync(Erasures[0]), host.Services.DeliverAsync(Erasures[0]));

        Assert.Empty(await storage.PendingLedgerAsync(ActionType.RemoveFamily, family.FamilyId.ToString("N")));
        Assert.Empty(await storage.FamilyRowsAsync(family.FamilyId));
    }

    [Fact]
    public async Task AMessageWithNoLedgerRow_TouchesNothing()
    {
        using var caller = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        var stray = new ActionMessage(
            "stray",
            ActionType.RemoveFamily,
            JsonSerializer.Serialize(new RemoveFamilyPayload(family.FamilyId), JsonSerializerOptions.Web)
        );

        await host.Services.DeliverAsync(stray);

        Assert.Equal(2, (await caller.Families.MeAsync(CancellationToken.None)).Children.Count);
        Assert.NotEmpty(await storage.BlobNamesAsync($"{family.FamilyId:D}/"));
    }

    public void Dispose() => host.Dispose();

    private async Task<Family> RegisteredAsync(FamilyCaller caller)
    {
        var family = await caller.RegisterAsync(BoardType.Cbse, 5, 3);
        await host.DeliverAsync();

        return family;
    }

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";
}
