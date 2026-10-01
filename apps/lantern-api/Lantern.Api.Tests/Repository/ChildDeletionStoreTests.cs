using Azure.Data.Tables;
using Lantern.Api.Exceptions;
using Lantern.Api.Models;
using Lantern.Api.Repository;
using Lantern.Api.Tests.Infrastructure;

namespace Lantern.Api.Tests.Repository;

[Collection(ApiCollection.Name)]
// Real Azurite: ETag claims and conditional writes are emulator behaviour a fake would hide.
public sealed class ChildDeletionStoreTests(AzuriteFixture azurite)
{
    private readonly TableClient families = new TableServiceClient(azurite.ConnectionString).GetTableClient(
        $"families{Guid.NewGuid():N}"
    );

    private ChildDeletionStore Store => new(families, createTables: true);

    [Fact]
    public async Task RequestAsync_MarksTheChildDeletingAndRecordsOneJob()
    {
        var (familyId, childId) = await SeedChildAsync();

        await Store.RequestAsync(familyId, childId, DateTimeOffset.UtcNow, CancellationToken.None);
        await Store.RequestAsync(familyId, childId, DateTimeOffset.UtcNow, CancellationToken.None);

        var row = await families.GetEntityAsync<TableEntity>(
            FamilyRepository.FamilyPartition(familyId),
            FamilyRepository.ChildRowKey(childId)
        );
        Assert.Equal("Deleting", row.Value.GetString("Status"));
        Assert.Single(await families.QueryAsync<TableEntity>(r => r.PartitionKey == ChildDeletionStore.PendingPartition).ToListAsync());
    }

    [Fact]
    public async Task RequestAsync_UnknownChild_ThrowsAndRecordsNothing()
    {
        var (familyId, _) = await SeedChildAsync();

        await Assert.ThrowsAsync<ChildNotFoundException>(() =>
            Store.RequestAsync(familyId, Guid.NewGuid(), DateTimeOffset.UtcNow, CancellationToken.None)
        );

        Assert.Empty(await families.QueryAsync<TableEntity>(r => r.PartitionKey == ChildDeletionStore.PendingPartition).ToListAsync());
    }

    [Fact]
    public async Task ClaimNextAsync_ReturnsADueJobOnceThenHoldsItUnderLease()
    {
        var (familyId, childId) = await SeedChildAsync();
        var now = DateTimeOffset.UtcNow;
        await Store.RequestAsync(familyId, childId, now, CancellationToken.None);

        var job = await Store.ClaimNextAsync(now.AddSeconds(1), CancellationToken.None);
        var again = await Store.ClaimNextAsync(now.AddSeconds(2), CancellationToken.None);
        var afterLease = await Store.ClaimNextAsync(now.AddMinutes(5), CancellationToken.None);

        Assert.Equal(new PendingDelete(familyId, childId, 1), job);
        Assert.Null(again);
        Assert.Equal(2, afterLease!.Attempts);
    }

    [Fact]
    public async Task ClaimNextAsync_TenAtOnce_ExactlyOneGetsTheJob()
    {
        var (familyId, childId) = await SeedChildAsync();
        var now = DateTimeOffset.UtcNow;
        await Store.RequestAsync(familyId, childId, now, CancellationToken.None);

        var claims = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => Store.ClaimNextAsync(now.AddSeconds(1), CancellationToken.None))
        );

        Assert.Single(claims, claim => claim is not null);
    }

    [Fact]
    public async Task RemoveChildRowAsyncThenRemovePendingAsync_LeaveNothingAndAreRepeatable()
    {
        var (familyId, childId) = await SeedChildAsync();
        var now = DateTimeOffset.UtcNow;
        await Store.RequestAsync(familyId, childId, now, CancellationToken.None);
        var job = (await Store.ClaimNextAsync(now.AddSeconds(1), CancellationToken.None))!;

        await Store.RemoveChildRowAsync(job, CancellationToken.None);
        await Store.RemoveChildRowAsync(job, CancellationToken.None);
        await Store.RemovePendingAsync(job, CancellationToken.None);
        await Store.RemovePendingAsync(job, CancellationToken.None);

        Assert.Empty(await families.QueryAsync<TableEntity>(r => r.PartitionKey == ChildDeletionStore.PendingPartition).ToListAsync());
        Assert.Empty(
            await families
                .QueryAsync<TableEntity>(r => r.PartitionKey == FamilyRepository.FamilyPartition(familyId) && r.RowKey == FamilyRepository.ChildRowKey(childId))
                .ToListAsync()
        );
    }

    private async Task<(Guid FamilyId, Guid ChildId)> SeedChildAsync()
    {
        await families.CreateIfNotExistsAsync();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        await families.AddEntityAsync(
            new TableEntity(FamilyRepository.FamilyPartition(familyId), FamilyRepository.ChildRowKey(childId))
            {
                ["NameCipher"] = "cipher",
                ["Status"] = nameof(ChildStatus.Active),
            }
        );

        return (familyId, childId);
    }
}
