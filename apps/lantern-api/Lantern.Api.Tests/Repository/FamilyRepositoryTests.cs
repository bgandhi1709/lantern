using Azure.Data.Tables;
using Lantern.Api.Models;
using Lantern.Api.Repository;
using Lantern.Api.Tests.Infrastructure;

namespace Lantern.Api.Tests.Repository;

[Collection(ApiCollection.Name)]
// Real Azurite: batch atomicity and 409 behaviour are emulator behaviour a fake would hide.
public sealed class FamilyRepositoryTests(AzuriteFixture azurite)
{
    private readonly TableClient parents = new TableServiceClient(azurite.ConnectionString).GetTableClient(
        $"parents{Guid.NewGuid():N}"
    );

    private readonly TableClient families = new TableServiceClient(azurite.ConnectionString).GetTableClient(
        $"families{Guid.NewGuid():N}"
    );

    private FamilyRepository Repository => new(parents, families, createTables: true);

    [Fact]
    public async Task TryRegisterAsync_ThenGet_ReturnsParentFamilyAndChildrenInOrder()
    {
        var pk = NewKey();
        var family = NewFamily();
        var first = NewChild(0);
        var second = NewChild(1);

        Assert.True(await Repository.TryRegisterAsync(NewParent(pk, family), family, [second, first], CancellationToken.None));

        var stored = await Repository.GetAsync(pk, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(pk, stored.Parent.PartitionKey);
        Assert.Equal(family.FamilyId, stored.Parent.FamilyId);
        Assert.Equal(family.FamilyId, stored.Family.FamilyId);
        Assert.Equal("Gujarat", stored.Family.Region);
        Assert.Equal([first.ChildId, second.ChildId], stored.Children.Select(child => child.ChildId));
    }

    [Fact]
    public async Task TryRegisterAsync_TenAtOnce_ExactlyOneWinsAndLosersLeaveNoFamilyBehind()
    {
        var pk = NewKey();

        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 10)
                .Select(_ =>
                {
                    var family = NewFamily();
                    return Repository.TryRegisterAsync(NewParent(pk, family), family, [NewChild(0)], CancellationToken.None);
                })
        );

        Assert.Equal(1, results.Count(won => won));
        var familyIds = new HashSet<string>();
        await foreach (var entity in families.QueryAsync<TableEntity>())
        {
            familyIds.Add(entity.PartitionKey);
        }

        Assert.Single(familyIds);
        Assert.Single((await Repository.GetAsync(pk, CancellationToken.None))!.Children);
    }

    [Fact]
    public async Task TryRegisterAsync_KeepsEachChildInTheFamilyPartitionOnly()
    {
        var pk = NewKey();
        var family = NewFamily();
        await Repository.TryRegisterAsync(NewParent(pk, family), family, [NewChild(0), NewChild(1)], CancellationToken.None);

        var parentRows = new List<string>();
        await foreach (var entity in parents.QueryAsync<TableEntity>(row => row.PartitionKey == pk))
        {
            parentRows.Add(entity.RowKey);
        }

        var familyRows = new List<string>();
        await foreach (var entity in families.QueryAsync<TableEntity>(row => row.PartitionKey == family.FamilyId.ToString("D")))
        {
            familyRows.Add(entity.RowKey.Split('_')[0]);
        }

        Assert.Equal(["profile"], parentRows);
        Assert.Equal(["child", "child", "family", "parent"], familyRows.Order());
    }

    [Fact]
    public async Task TryRegisterAsync_WhenAlreadyRegistered_ReturnsFalseAndWritesNothing()
    {
        var pk = NewKey();
        var family = NewFamily();
        var original = NewChild(0);
        await Repository.TryRegisterAsync(NewParent(pk, family), family, [original], CancellationToken.None);
        var again = NewFamily();

        var registeredAgain = await Repository.TryRegisterAsync(NewParent(pk, again), again, [NewChild(0)], CancellationToken.None);

        Assert.False(registeredAgain);
        var stored = await Repository.GetAsync(pk, CancellationToken.None);
        Assert.Equal(family.FamilyId, stored!.Family.FamilyId);
        Assert.Equal([original.ChildId], stored.Children.Select(child => child.ChildId));
        Assert.Empty(await families.QueryAsync<TableEntity>(row => row.PartitionKey == again.FamilyId.ToString("D")).ToListAsync());
    }

    [Fact]
    public async Task GetAsync_ReturnsOnlyThatParentsFamily()
    {
        var mine = NewKey();
        var theirs = NewKey();
        var myFamily = NewFamily();
        var theirFamily = NewFamily();
        await Repository.TryRegisterAsync(NewParent(mine, myFamily), myFamily, [NewChild(0)], CancellationToken.None);
        await Repository.TryRegisterAsync(NewParent(theirs, theirFamily), theirFamily, [NewChild(0), NewChild(0)], CancellationToken.None);

        Assert.Single((await Repository.GetAsync(mine, CancellationToken.None))!.Children);
    }

    [Fact]
    public async Task GetAsync_Unknown_ReturnsNull() =>
        Assert.Null(await Repository.GetAsync(NewKey(), CancellationToken.None));

    [Fact]
    public async Task TryRegisterAsync_StoresTimesAsUtc()
    {
        var pk = NewKey();
        var family = NewFamily();
        var parent = NewParent(pk, family) with { ConsentAt = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.FromHours(5.5)) };
        await Repository.TryRegisterAsync(parent, family, [NewChild(0)], CancellationToken.None);

        var stored = await Repository.GetAsync(pk, CancellationToken.None);

        Assert.Equal(parent.ConsentAt, stored!.Parent.ConsentAt);
        Assert.Equal(TimeSpan.Zero, stored.Parent.ConsentAt.Offset);
    }

    [Fact]
    public async Task WithoutCreateTables_MissingTables_FailsAndNothingIsCreated()
    {
        var service = new TableServiceClient(azurite.ConnectionString);
        var parentsName = $"parents{Guid.NewGuid():N}";
        var familiesName = $"families{Guid.NewGuid():N}";
        var azureStyle = new FamilyRepository(
            service.GetTableClient(parentsName),
            service.GetTableClient(familiesName),
            createTables: false
        );
        var family = NewFamily();

        // A batch submission against a missing table fails as a TableTransactionFailedException,
        // not the plain RequestFailedException a single-entity call would throw.
        var register = await Assert.ThrowsAsync<TableTransactionFailedException>(() =>
            azureStyle.TryRegisterAsync(NewParent(NewKey(), family), family, [NewChild(0)], CancellationToken.None)
        );
        var read = await Assert.ThrowsAsync<Azure.RequestFailedException>(() =>
            azureStyle.GetAsync(NewKey(), CancellationToken.None)
        );

        Assert.Equal(404, register.Status);
        Assert.Equal(404, read.Status);
        Assert.DoesNotContain(service.Query(table => table.Name == parentsName || table.Name == familiesName), _ => true);
    }

    [Fact]
    public async Task WithoutCreateTables_ExistingTables_Works()
    {
        var service = new TableServiceClient(azurite.ConnectionString);
        var parentsName = $"parents{Guid.NewGuid():N}";
        var familiesName = $"families{Guid.NewGuid():N}";
        await service.CreateTableAsync(parentsName);
        await service.CreateTableAsync(familiesName);
        var azureStyle = new FamilyRepository(
            service.GetTableClient(parentsName),
            service.GetTableClient(familiesName),
            createTables: false
        );
        var pk = NewKey();
        var family = NewFamily();

        Assert.True(await azureStyle.TryRegisterAsync(NewParent(pk, family), family, [NewChild(0)], CancellationToken.None));
        Assert.Single((await azureStyle.GetAsync(pk, CancellationToken.None))!.Children);
    }

    private static string NewKey() => Guid.NewGuid().ToString("N");

    private static FamilyRecord NewFamily() =>
        new(Guid.NewGuid(), "Gujarat", "wrapped-key", "keyvault", DateTimeOffset.UtcNow);

    private static ParentProfile NewParent(string pk, FamilyRecord family) =>
        new(pk, Guid.NewGuid(), family.FamilyId, "cipher-name", "cipher-email", "gu", "2026-09", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static ChildRecord NewChild(int position) =>
        new(Guid.NewGuid(), "cipher-child", null, 1, 2020, position, DateTimeOffset.UtcNow);
}
