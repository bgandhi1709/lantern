using Azure.Data.Tables;
using Lantern.Api.Models;
using Lantern.Api.Repository;
using Lantern.Api.Tests.Infrastructure;

namespace Lantern.Api.Tests.Repository;

[Collection(ApiCollection.Name)]
// Real Azurite: batch atomicity and 409 behaviour are emulator behaviour a fake would hide.
public sealed class TableParentRepositoryTests(AzuriteFixture azurite)
{
    private readonly TableParentRepository repository = new(
        new TableServiceClient(azurite.ConnectionString).GetTableClient($"parents{Guid.NewGuid():N}"),
        createTable: true
    );

    [Fact]
    public async Task TryRegisterAsync_ThenGet_ReturnsProfileAndChildrenInOrder()
    {
        var pk = NewKey();
        var first = NewChild(0);
        var second = NewChild(1);

        Assert.True(await this.repository.TryRegisterAsync(NewProfile(pk), [second, first], CancellationToken.None));

        var stored = await this.repository.GetAsync(pk, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(pk, stored.Profile.PartitionKey);
        Assert.Equal([first.ChildId, second.ChildId], stored.Children.Select(child => child.ChildId));
    }

    [Fact]
    public async Task TryRegisterAsync_WhenAlreadyRegistered_ReturnsFalseAndWritesNothing()
    {
        var pk = NewKey();
        var original = NewChild(0);
        await this.repository.TryRegisterAsync(NewProfile(pk), [original], CancellationToken.None);

        var again = await this.repository.TryRegisterAsync(NewProfile(pk), [NewChild(0)], CancellationToken.None);

        Assert.False(again);
        var stored = await this.repository.GetAsync(pk, CancellationToken.None);
        Assert.Equal([original.ChildId], stored!.Children.Select(child => child.ChildId));
    }

    [Fact]
    public async Task TryRegisterAsync_TenAtOnce_ExactlyOneWins()
    {
        var pk = NewKey();

        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 10)
                .Select(_ => this.repository.TryRegisterAsync(NewProfile(pk), [NewChild(0)], CancellationToken.None))
        );

        Assert.Equal(1, results.Count(won => won));
        Assert.Single((await this.repository.GetAsync(pk, CancellationToken.None))!.Children);
    }

    [Fact]
    public async Task GetAsync_ReturnsOnlyThatMothersRows()
    {
        var mine = NewKey();
        var theirs = NewKey();
        await this.repository.TryRegisterAsync(NewProfile(mine), [NewChild(0)], CancellationToken.None);
        await this.repository.TryRegisterAsync(NewProfile(theirs), [NewChild(0), NewChild(0)], CancellationToken.None);

        Assert.Single((await this.repository.GetAsync(mine, CancellationToken.None))!.Children);
    }

    [Fact]
    public async Task GetAsync_Unknown_ReturnsNull() =>
        Assert.Null(await this.repository.GetAsync(NewKey(), CancellationToken.None));

    [Fact]
    public async Task TryRegisterAsync_StoresTimesAsUtc()
    {
        var pk = NewKey();
        var profile = NewProfile(pk) with { ConsentAt = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.FromHours(5.5)) };
        await this.repository.TryRegisterAsync(profile, [NewChild(0)], CancellationToken.None);

        var stored = await this.repository.GetAsync(pk, CancellationToken.None);

        Assert.Equal(profile.ConsentAt, stored!.Profile.ConsentAt);
        Assert.Equal(TimeSpan.Zero, stored.Profile.ConsentAt.Offset);
    }

    [Fact]
    public async Task WithoutCreateTable_MissingTable_FailsAndIsNotCreated()
    {
        var name = $"parents{Guid.NewGuid():N}";
        var service = new TableServiceClient(azurite.ConnectionString);
        var azureStyle = new TableParentRepository(service.GetTableClient(name), createTable: false);

        // A batch submission against a missing table fails as a TableTransactionFailedException,
        // not the plain RequestFailedException a single-entity call would throw.
        var register = await Assert.ThrowsAsync<Azure.Data.Tables.TableTransactionFailedException>(() =>
            azureStyle.TryRegisterAsync(NewProfile(NewKey()), [NewChild(0)], CancellationToken.None)
        );
        var read = await Assert.ThrowsAsync<Azure.RequestFailedException>(() =>
            azureStyle.GetAsync(NewKey(), CancellationToken.None)
        );

        Assert.Equal(404, register.Status);
        Assert.Equal(404, read.Status);
        Assert.DoesNotContain(service.Query(table => table.Name == name), _ => true);
    }

    [Fact]
    public async Task WithoutCreateTable_ExistingTable_Works()
    {
        var name = $"parents{Guid.NewGuid():N}";
        var service = new TableServiceClient(azurite.ConnectionString);
        await service.CreateTableAsync(name);
        var azureStyle = new TableParentRepository(service.GetTableClient(name), createTable: false);
        var pk = NewKey();

        Assert.True(await azureStyle.TryRegisterAsync(NewProfile(pk), [NewChild(0)], CancellationToken.None));
        Assert.Single((await azureStyle.GetAsync(pk, CancellationToken.None))!.Children);
    }

    private static string NewKey() => Guid.NewGuid().ToString("N");

    private static ParentProfile NewProfile(string pk) =>
        new(pk, Guid.NewGuid(), "cipher-name", "cipher-email", "Gujarat", "gu", "2026-09", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static ChildRecord NewChild(int position) =>
        new(Guid.NewGuid(), "cipher-child", null, 1, 2020, position, DateTimeOffset.UtcNow);
}
