using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Functions.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lantern.Functions.Tests;

[Collection(FunctionsCollection.Name)]
public sealed class ChildActionTests(AzuriteFixture azurite) : IDisposable
{
    private readonly FunctionsHost host = new(azurite.ConnectionString);

    private readonly StorageProbe storage = new(azurite);

    private IReadOnlyList<ActionMessage> Removals => [.. host.Sender.Sent.Where(m => m.Type == ActionType.RemoveWorkspace)];

    [Fact]
    public async Task Add_WhenTheWorkspaceFails_KeepsTheChild_AndRedeliveryStartsIt()
    {
        using var caller = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        var id = Guid.NewGuid();
        var workspaces = new Mock<IWorkspaceStore>();
        workspaces
            .Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), 9, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, "blob down"));
        using var failing = new FunctionsHost(azurite.ConnectionString, configure: services => services.AddSingleton(workspaces.Object));

        await caller.Children.AddAsync(NewChild(id, 9), CancellationToken.None);
        await Assert.ThrowsAsync<RequestFailedException>(() => failing.Services.DeliverAsync(host.Sender.Sent[^1]));
        await host.DeliverAsync();

        Assert.Equal(3, (await caller.Families.MeAsync(CancellationToken.None)).Children.Count);
        Assert.True(await storage.Blobs.GetBlobClient($"{family.FamilyId:D}/{id:D}/9/class.json").ExistsAsync());
    }

    [Fact]
    public async Task Delete_RemovesEverythingOfTheChild_AndNothingOfItsSibling()
    {
        using var caller = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        var (gone, kept) = (family.Children[0], family.Children[1]);
        await PlainWorkspaceStore.Over(storage.Blobs, createContainer: false).CreateAsync(family.FamilyId, gone.ChildId, 6, CancellationToken.None);

        await caller.Children.RemoveAsync(gone.ChildId, CancellationToken.None);

        Assert.Single(await storage.PendingRemoveWorkspaceLedgerAsync(family.FamilyId));
        Assert.True(await storage.Blobs.GetBlobClient($"{family.FamilyId:D}/{gone.ChildId:D}/{gone.ClassLevel}/class.json").ExistsAsync());

        await host.DeliverAsync();

        Assert.Empty(await storage.BlobNamesAsync($"{family.FamilyId:D}/{gone.ChildId:D}/"));
        Assert.True(await storage.Blobs.GetBlobClient($"{family.FamilyId:D}/{kept.ChildId:D}/{kept.ClassLevel}/class.json").ExistsAsync());
        Assert.Empty(await storage.PendingRemoveWorkspaceLedgerAsync(family.FamilyId));
        Assert.Equal("remove-workspace", StorageProbe.DoneBy(Assert.Single(await storage.RemoveWorkspaceLedgerAsync(family.FamilyId))));
        Assert.Empty(await storage.Families.QueryAsync<TableEntity>(r => r.PartitionKey == storage.Keys.FamilyPartition(family.FamilyId) && r.RowKey == storage.Keys.ChildRowKey(gone.ChildId)).ToListAsync());
    }

    [Fact]
    public async Task Delete_TheSameMessageDeliveredAtOnce_EndsAsIfDeliveredOnce_AndLeavesTheSiblingAlone()
    {
        using var caller = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        var (gone, kept) = (family.Children[0], family.Children[1]);
        await PlainWorkspaceStore.Over(storage.Blobs, createContainer: false).CreateAsync(family.FamilyId, gone.ChildId, 6, CancellationToken.None);
        await caller.Children.RemoveAsync(gone.ChildId, CancellationToken.None);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => host.Services.DeliverAsync(Removals[0])));

        Assert.Empty(await storage.BlobNamesAsync($"{family.FamilyId:D}/{gone.ChildId:D}/"));
        Assert.True(await storage.Blobs.GetBlobClient($"{family.FamilyId:D}/{kept.ChildId:D}/{kept.ClassLevel}/class.json").ExistsAsync());
        Assert.Empty(await storage.PendingRemoveWorkspaceLedgerAsync(family.FamilyId));
        Assert.Single((await caller.Families.MeAsync(CancellationToken.None)).Children);
    }

    [Fact]
    public async Task Delete_Repeated_SendsAgain_TheSecondDeliveryDoesNothing_AndTheChildIsGone()
    {
        using var caller = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        var child = family.Children[0];

        await caller.Children.RemoveAsync(child.ChildId, CancellationToken.None);
        await caller.Children.RemoveAsync(child.ChildId, CancellationToken.None);
        await host.DeliverAsync();
        var third = await Assert.ThrowsAsync<LanternException>(() => caller.Children.RemoveAsync(child.ChildId, CancellationToken.None));

        Assert.Equal(LanternErrorCode.ChildNotFound, third.Code);
        Assert.Equal(2, Removals.Count);
        Assert.Empty(await storage.PendingRemoveWorkspaceLedgerAsync(family.FamilyId));
    }

    [Fact]
    public async Task WhenTheBlobStepFails_KeepsTheRowAndTheLedger_AndFinishesWhenTheMessageIsDeliveredAgain()
    {
        using var caller = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        var child = family.Children[0];
        await caller.Children.RemoveAsync(child.ChildId, CancellationToken.None);
        var failing = new Mock<IWorkspaceStore>();
        failing
            .Setup(s => s.RemoveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "blob down"));
        using var broken = new FunctionsHost(azurite.ConnectionString, configure: services => services.AddSingleton(failing.Object));

        await Assert.ThrowsAsync<RequestFailedException>(() => broken.Services.DeliverAsync(Removals[0]));

        Assert.Single(await storage.PendingRemoveWorkspaceLedgerAsync(family.FamilyId));
        Assert.True(await storage.Families.GetEntityIfExistsAsync<TableEntity>(storage.Keys.FamilyPartition(family.FamilyId), storage.Keys.ChildRowKey(child.ChildId)) is { HasValue: true });
        Assert.NotEmpty(await storage.BlobNamesAsync($"{family.FamilyId:D}/{child.ChildId:D}/"));

        await host.DeliverAsync();

        Assert.Empty(await storage.PendingRemoveWorkspaceLedgerAsync(family.FamilyId));
        Assert.Empty(await storage.BlobNamesAsync($"{family.FamilyId:D}/{child.ChildId:D}/"));
    }

    [Fact]
    public async Task ResumesAfterACrashBetweenTheRowDeleteAndTheLastStep()
    {
        using var caller = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        var child = family.Children[0];
        await caller.Children.RemoveAsync(child.ChildId, CancellationToken.None);
        await storage.Blobs.DeleteBlobIfExistsAsync($"{family.FamilyId:D}/{child.ChildId:D}/{child.ClassLevel}/class.json");
        await storage.Families.DeleteEntityAsync(storage.Keys.FamilyPartition(family.FamilyId), storage.Keys.ChildRowKey(child.ChildId));

        await host.DeliverAsync();

        Assert.Empty(await storage.PendingRemoveWorkspaceLedgerAsync(family.FamilyId));
    }

    [Fact]
    public async Task TheSameMessageTwiceAtOnce_FinishesWithoutError()
    {
        using var caller = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        await caller.Children.RemoveAsync(family.Children[0].ChildId, CancellationToken.None);
        var message = Removals[0];

        await Task.WhenAll(host.Services.DeliverAsync(message), host.Services.DeliverAsync(message));

        Assert.Empty(await storage.PendingRemoveWorkspaceLedgerAsync(family.FamilyId));
        Assert.Empty(await storage.BlobNamesAsync($"{family.FamilyId:D}/{family.Children[0].ChildId:D}/"));
    }

    [Fact]
    public async Task AMessageWithNoLedgerRow_TouchesNothing_AndTheAnswerLibraryIsNeverReached()
    {
        using var caller = host.Caller(NewUid());
        using var other = host.Caller(NewUid());
        var family = await RegisteredAsync(caller);
        var otherFamily = await RegisteredAsync(other);
        await storage.Blobs.GetBlobClient("answers/stage-3/q1.json").UploadAsync(BinaryData.FromString("{}"), overwrite: true);
        await storage.Families.UpsertEntityAsync(new TableEntity("answer-library", "q1") { ["Text"] = "scrubbed" });
        var stray = new ActionMessage(
            "stray",
            ActionType.RemoveWorkspace,
            JsonSerializer.Serialize(new RemoveWorkspacePayload(otherFamily.FamilyId, otherFamily.Children[0].ChildId), JsonSerializerOptions.Web)
        );
        await caller.Children.RemoveAsync(family.Children[0].ChildId, CancellationToken.None);

        await host.Services.DeliverAsync(stray);
        await host.DeliverAsync();

        Assert.NotEmpty(await storage.BlobNamesAsync($"{otherFamily.FamilyId:D}/{otherFamily.Children[0].ChildId:D}/"));
        Assert.True(await storage.Blobs.GetBlobClient("answers/stage-3/q1.json").ExistsAsync());
        Assert.True((await storage.Families.GetEntityIfExistsAsync<TableEntity>("answer-library", "q1")).HasValue);
        Assert.Equal(2, (await other.Families.MeAsync(CancellationToken.None)).Children.Count);
    }

    public void Dispose() => host.Dispose();

    private async Task<Family> RegisteredAsync(FamilyCaller caller)
    {
        var family = await caller.RegisterAsync(BoardType.Cbse, 5, 3);
        await host.DeliverAsync();

        return family;
    }

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Child NewChild(Guid id, int classLevel) =>
        new() { ChildId = id, Name = "locked-name", ClassLevel = classLevel, BirthYear = $"locked-{DateTime.UtcNow.Year - 8}", School = "locked-school" };
}
