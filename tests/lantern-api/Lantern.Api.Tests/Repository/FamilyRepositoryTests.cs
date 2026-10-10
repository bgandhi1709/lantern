using Azure;
using Azure.Data.Tables;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;

namespace Lantern.Api.Tests.Repository;

[Collection(ApiCollection.Name)]
// Real Azurite: batch atomicity and 409 behaviour are emulator behaviour a fake would hide.
public sealed class FamilyRepositoryTests(AzuriteFixture azurite) : IDisposable
{
    private readonly RepositoryHarness harness = new(azurite.ConnectionString);

    [Fact]
    public async Task RegisterAsync_ThenRead_ReturnsParentFamilyAndChildrenInOrder()
    {
        var uid = NewUid();
        var family = NewFamily();
        var first = NewChild(family, 0, "Aarav");
        var second = NewChild(family, 1, "Diya");

        await harness.FamilyRepository().RegisterAsync(uid, family, NewParent(family), [second, first], CancellationToken.None);

        var parent = await harness.FamilyRepository().FindParentAsync(uid, CancellationToken.None);
        var stored = await harness.FamilyRepository().SingleAsync(family.FamilyId, family.FamilyId, CancellationToken.None);
        var children = await harness.ChildRepository().CollectionAsync(family.FamilyId, CancellationToken.None);

        Assert.Equal(("locked:Meena", "locked:meena@example.test", family.FamilyId), (parent!.Name, parent.Email, parent.FamilyId));
        Assert.Equal(("Gujarat", BoardType.Ssc), (stored.Region, stored.Board));
        Assert.Equal(("passphrase-key", "passphrase-salt", "recovery-key", "recovery-salt"), (stored.PassphraseWrappedKey, stored.PassphraseSalt, stored.RecoveryWrappedKey, stored.RecoverySalt));
        Assert.Equal(["Aarav", "Diya"], children.Select(child => child.Name));
    }

    [Fact]
    public async Task RegisterAsync_TenAtOnce_ExactlyOneWinsAndLosersLeaveNoFamilyBehind()
    {
        var uid = NewUid();

        var attempts = Enumerable
            .Range(0, 10)
            .Select(async _ =>
            {
                var family = NewFamily();
                try
                {
                    await harness.FamilyRepository().RegisterAsync(uid, family, NewParent(family), [NewChild(family, 0)], CancellationToken.None);
                    return true;
                }
                catch (LanternException ex) when (ex.Code == LanternErrorCode.AlreadyRegistered)
                {
                    return false;
                }
            });

        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(won => won));
        Assert.Single((await harness.Families.QueryAsync<TableEntity>().ToListAsync()).Select(row => row.PartitionKey).Distinct());
    }

    [Fact]
    public async Task RegisterAsync_KeepsEachChildInTheFamilyPartitionOnly()
    {
        var uid = NewUid();
        var family = NewFamily();
        await harness.FamilyRepository().RegisterAsync(uid, family, NewParent(family), [NewChild(family, 0), NewChild(family, 1)], CancellationToken.None);

        var parentRows = await harness.Parents.QueryAsync<TableEntity>().Select(row => row.RowKey).ToListAsync();
        var familyRows = await harness
            .Families.QueryAsync<TableEntity>(row => row.PartitionKey == family.FamilyId.ToString("D"))
            .Select(row => row.RowKey.Split('_')[0])
            .ToListAsync();

        Assert.Equal(["profile"], parentRows);
        Assert.Equal(["child", "child", "family", "parent"], familyRows.Order());
    }

    [Fact]
    public async Task RegisterAsync_StoresTheLockedValuesAndKeysAsSent_AndNoUid()
    {
        var uid = NewUid();
        var family = NewFamily();
        var parent = NewParent(family);
        var child = NewChild(family, 0, "Aarav");
        await harness.FamilyRepository().RegisterAsync(uid, family, parent, [child], CancellationToken.None);

        var rows = await harness.Families.QueryAsync<TableEntity>().ToListAsync();
        rows.AddRange(await harness.Parents.QueryAsync<TableEntity>().ToListAsync());
        var values = rows.SelectMany(row => ((IDictionary<string, object>)row).Values).OfType<string>().ToList();

        foreach (var sent in new[] { parent.Name, parent.Email, child.Name, child.BirthYear, family.PassphraseWrappedKey, family.RecoveryWrappedKey })
        {
            Assert.Contains(sent, values);
        }

        Assert.DoesNotContain(values, value => value.Contains(uid, StringComparison.Ordinal));
        Assert.Equal("Ssc", rows.Single(row => row.RowKey == "family").GetString("Board"));
    }

    [Fact]
    public async Task RegisterAsync_WhenTheFamilyIdIsTaken_Throws_AndLeavesTheFirstFamilyAlone()
    {
        var family = NewFamily();
        await harness.FamilyRepository().RegisterAsync(NewUid(), family, NewParent(family), [NewChild(family, 0, "Aarav")], CancellationToken.None);
        var intruderUid = NewUid();
        var intruder = NewFamily();
        intruder.FamilyId = family.FamilyId;
        intruder.PassphraseWrappedKey = "intruder-key";

        await Errors.ThrowsAsync(LanternErrorCode.FamilyIdTaken, () =>
            harness.FamilyRepository().RegisterAsync(intruderUid, intruder, NewParent(intruder), [NewChild(intruder, 0, "Intruder")], CancellationToken.None)
        );

        Assert.Null(await harness.FamilyRepository().FindParentAsync(intruderUid, CancellationToken.None));
        var stored = await harness.FamilyRepository().SingleAsync(family.FamilyId, family.FamilyId, CancellationToken.None);
        Assert.Equal("passphrase-key", stored.PassphraseWrappedKey);
        Assert.Equal(["Aarav"], (await harness.ChildRepository().CollectionAsync(family.FamilyId, CancellationToken.None)).Select(child => child.Name));
    }

    [Fact]
    public async Task RegisterAsync_WhenAlreadyRegistered_ThrowsAndWritesNothing()
    {
        var uid = NewUid();
        var family = NewFamily();
        await harness.FamilyRepository().RegisterAsync(uid, family, NewParent(family), [NewChild(family, 0)], CancellationToken.None);
        var again = NewFamily();

        await Errors.ThrowsAsync(LanternErrorCode.AlreadyRegistered, () =>
            harness.FamilyRepository().RegisterAsync(uid, again, NewParent(again), [NewChild(again, 0)], CancellationToken.None)
        );

        Assert.Equal(family.FamilyId, (await harness.FamilyRepository().FindParentAsync(uid, CancellationToken.None))!.FamilyId);
        Assert.Empty(await harness.Families.QueryAsync<TableEntity>(row => row.PartitionKey == again.FamilyId.ToString("D")).ToListAsync());
    }

    [Fact]
    public async Task FindParentAsync_Unknown_ReturnsNull() =>
        Assert.Null(await harness.FamilyRepository().FindParentAsync(NewUid(), CancellationToken.None));

    [Fact]
    public async Task RegisterAsync_StoresTimesAsUtc()
    {
        var uid = NewUid();
        var family = NewFamily();
        var parent = NewParent(family);
        parent.ConsentAt = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.FromHours(5.5));
        await harness.FamilyRepository().RegisterAsync(uid, family, parent, [NewChild(family, 0)], CancellationToken.None);

        var stored = await harness.FamilyRepository().FindParentAsync(uid, CancellationToken.None);

        Assert.Equal(parent.ConsentAt, stored!.ConsentAt);
        Assert.Equal(TimeSpan.Zero, stored.ConsentAt.Offset);
    }

    // Families registered before the Board existed have no Board column; they follow CBSE and need no migration.
    [Fact]
    public async Task AFamilyRowWithNoBoard_ReadsAsCbse()
    {
        var familyId = Guid.NewGuid();
        await harness.Families.CreateIfNotExistsAsync();
        await harness.Families.AddEntityAsync(new TableEntity(familyId.ToString("D"), "family")
        {
            ["FamilyId"] = familyId,
            ["Region"] = "Gujarat",
            ["CreatedAt"] = DateTimeOffset.UtcNow,
        });

        var stored = await harness.FamilyRepository().SingleAsync(familyId, familyId, CancellationToken.None);

        Assert.Equal(BoardType.Cbse, stored.Board);
    }

    [Fact]
    public async Task WithoutCreateTables_MissingTables_FailsAndNothingIsCreated()
    {
        var service = new TableServiceClient(azurite.ConnectionString);
        using var azureStyle = new RepositoryHarness(azurite.ConnectionString, createTables: false);
        var family = NewFamily();

        var register = await Assert.ThrowsAnyAsync<RequestFailedException>(() =>
            azureStyle.FamilyRepository().RegisterAsync(NewUid(), family, NewParent(family), [NewChild(family, 0)], CancellationToken.None)
        );
        var read = await Assert.ThrowsAnyAsync<RequestFailedException>(() =>
            azureStyle.FamilyRepository().FindParentAsync(NewUid(), CancellationToken.None)
        );

        Assert.Equal((404, 404), (register.Status, read.Status));
        Assert.DoesNotContain(service.Query(table => table.Name == azureStyle.Parents.Name || table.Name == azureStyle.Families.Name), _ => true);
    }

    [Fact]
    public async Task WithoutCreateTables_ExistingTables_Works()
    {
        var service = new TableServiceClient(azurite.ConnectionString);
        var parentsName = $"parents{Guid.NewGuid():N}";
        var familiesName = $"families{Guid.NewGuid():N}";
        await service.CreateTableAsync(parentsName);
        await service.CreateTableAsync(familiesName);
        using var azureStyle = new RepositoryHarness(azurite.ConnectionString, createTables: false, parentsName, familiesName);
        var uid = NewUid();
        var family = NewFamily();

        await azureStyle.FamilyRepository().RegisterAsync(uid, family, NewParent(family), [NewChild(family, 0)], CancellationToken.None);

        Assert.NotNull(await azureStyle.FamilyRepository().FindParentAsync(uid, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveParentsAsync_RemovesTheFamilysProfiles_AndKeepsItsRowsAndOtherFamilies()
    {
        var (uid, family) = await RegisteredAsync();
        var (otherUid, _) = await RegisteredAsync();

        await harness.FamilyRepository().RemoveParentsAsync(family.FamilyId, CancellationToken.None);

        Assert.Null(await harness.FamilyRepository().FindParentAsync(uid, CancellationToken.None));
        Assert.NotNull(await harness.FamilyRepository().FindParentAsync(otherUid, CancellationToken.None));
        Assert.NotEmpty(await PartitionAsync(family));
    }

    [Fact]
    public async Task EraseAsync_RemovesEveryRowOfTheFamily_IsSafeToRepeat_AndKeepsOtherFamilies()
    {
        var (uid, family) = await RegisteredAsync();
        var (otherUid, other) = await RegisteredAsync();

        await harness.FamilyRepository().EraseAsync(family.FamilyId, CancellationToken.None);
        await harness.FamilyRepository().EraseAsync(family.FamilyId, CancellationToken.None);

        Assert.Empty(await PartitionAsync(family));
        Assert.Null(await harness.FamilyRepository().FindParentAsync(uid, CancellationToken.None));
        Assert.NotEmpty(await PartitionAsync(other));
        Assert.NotNull(await harness.FamilyRepository().FindParentAsync(otherUid, CancellationToken.None));
    }

    [Fact]
    public async Task EraseAsync_LeavesAProfileThatNowBelongsToAnotherFamily()
    {
        var (uid, family) = await RegisteredAsync();
        await harness.FamilyRepository().RemoveParentsAsync(family.FamilyId, CancellationToken.None);
        var again = NewFamily();
        await harness.FamilyRepository().RegisterAsync(uid, again, NewParent(again), [NewChild(again, 0)], CancellationToken.None);

        await harness.FamilyRepository().EraseAsync(family.FamilyId, CancellationToken.None);

        Assert.Equal(again.FamilyId, (await harness.FamilyRepository().FindParentAsync(uid, CancellationToken.None))!.FamilyId);
    }

    private async Task<(string Uid, Family Family)> RegisteredAsync()
    {
        var uid = NewUid();
        var family = NewFamily();
        await harness.FamilyRepository().RegisterAsync(uid, family, NewParent(family), [NewChild(family, 0)], CancellationToken.None);

        return (uid, family);
    }

    private async Task<List<TableEntity>> PartitionAsync(Family family) =>
        await harness.Families.QueryAsync<TableEntity>(row => row.PartitionKey == family.FamilyId.ToString("D")).ToListAsync();

    private static string HarnessChildRow(Child child) => RepositoryHarness.KeyService.ChildRowKey(child.ChildId);

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    internal static Family NewFamily() =>
        new()
        {
            FamilyId = Guid.NewGuid(),
            Region = "Gujarat",
            Board = BoardType.Ssc,
            PassphraseWrappedKey = "passphrase-key",
            PassphraseSalt = "passphrase-salt",
            RecoveryWrappedKey = "recovery-key",
            RecoverySalt = "recovery-salt",
            CreatedAt = DateTimeOffset.UtcNow,
        };

    internal static Parent NewParent(Family family) =>
        new()
        {
            ParentId = Guid.NewGuid(),
            FamilyId = family.FamilyId,
            Name = "locked:Meena",
            Email = "locked:meena@example.test",
            Language = "gu",
            ConsentVersion = "2026-09",
            ConsentAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    internal static Child NewChild(Family family, int position, string name = "Kavya") =>
        new()
        {
            FamilyId = family.FamilyId,
            ChildId = Guid.NewGuid(),
            Name = name,
            ClassLevel = 1,
            BirthYear = "locked:2020",
            Position = position,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ChildStatus.Active,
        };

    public void Dispose() => harness.Dispose();
}
