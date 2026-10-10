using Azure.Data.Tables;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using static Lantern.Api.Tests.Repository.FamilyRepositoryTests;

namespace Lantern.Api.Tests.Repository;

[Collection(ApiCollection.Name)]
// Real Azurite: ETag conflicts and merges on missing rows are table behaviour a fake would hide.
public sealed class ChildRepositoryTests(AzuriteFixture azurite) : IDisposable
{
    private readonly RepositoryHarness harness = new(azurite.ConnectionString);

    [Fact]
    public async Task AddAsync_TenAtOnceAtFive_ExactlyOneWins()
    {
        var family = await RegisteredAsync(5);

        var attempts = Enumerable
            .Range(0, 10)
            .Select(async _ =>
            {
                try
                {
                    await harness.ChildRepository().AddAsync(NewChild(family, 0), CancellationToken.None);
                    return true;
                }
                catch (Exception ex) when (ex is FamilyChangedException or ChildLimitReachedException)
                {
                    return false;
                }
            });

        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(won => won));
        Assert.Equal(6, (await harness.ChildRepository().CollectionAsync(family.FamilyId, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task AddAsync_AtSix_ThrowsChildLimitReached()
    {
        var family = await RegisteredAsync(6);

        await Assert.ThrowsAsync<ChildLimitReachedException>(() =>
            harness.ChildRepository().AddAsync(NewChild(family, 0), CancellationToken.None)
        );
    }

    [Fact]
    public async Task AddAsync_SetsThePositionAfterTheOthers_AndAnExistingIdThrows()
    {
        var family = await RegisteredAsync(2);
        var added = NewChild(family, 0, "Kavya");

        await harness.ChildRepository().AddAsync(added, CancellationToken.None);

        Assert.Equal(2, added.Position);
        Assert.Equal("Kavya", (await harness.ChildRepository().CollectionAsync(family.FamilyId, CancellationToken.None))[^1].NameLocked);
        await Assert.ThrowsAsync<FamilyChangedException>(() => harness.ChildRepository().AddAsync(added, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_ChangesFieldsAndClearsSchool_KeepsClassAndStatus()
    {
        var family = NewFamily();
        var child = NewChild(family, 0);
        child.SchoolLocked = "Green School";
        child.ClassLevel = 6;
        await harness.FamilyRepository().RegisterAsync(NewUid(), family, NewParent(family), [child], CancellationToken.None);

        await harness.ChildRepository().UpdateAsync(
            new Child { FamilyId = family.FamilyId, ChildId = child.ChildId, NameLocked = "New Name", BirthYearLocked = "2019", ClassLevel = 9 },
            CancellationToken.None
        );

        var stored = Assert.Single(await harness.ChildRepository().CollectionAsync(family.FamilyId, CancellationToken.None));
        Assert.Equal(("New Name", (string?)null, "2019", 6, ChildStatus.Active), (stored.NameLocked, stored.SchoolLocked, stored.BirthYearLocked, stored.ClassLevel, stored.Status));
    }

    [Fact]
    public async Task UpdateAsync_MissingOrDeleting_ThrowsNotFound()
    {
        var family = NewFamily();
        var child = NewChild(family, 0);
        await harness.FamilyRepository().RegisterAsync(NewUid(), family, NewParent(family), [child], CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            harness.ChildRepository().UpdateAsync(NewChild(family, 0), CancellationToken.None)
        );

        await harness.ChildRepository().MarkDeletingAsync(family.FamilyId, child.ChildId, CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => harness.ChildRepository().UpdateAsync(child, CancellationToken.None));
    }

    [Fact]
    public async Task MarkDeletingAsync_SetsTheStatus_KeepsTheRestOfTheRow_AndIsRepeatable()
    {
        var family = NewFamily();
        var child = NewChild(family, 0, "Aarav");
        await harness.FamilyRepository().RegisterAsync(NewUid(), family, NewParent(family), [child], CancellationToken.None);

        await harness.ChildRepository().MarkDeletingAsync(family.FamilyId, child.ChildId, CancellationToken.None);
        await harness.ChildRepository().MarkDeletingAsync(family.FamilyId, child.ChildId, CancellationToken.None);

        var stored = await harness.ChildRepository().SingleAsync(family.FamilyId, child.ChildId, CancellationToken.None);
        Assert.Equal((ChildStatus.Deleting, "Aarav"), (stored.Status, stored.NameLocked));
    }

    [Fact]
    public async Task MarkDeletingAsync_ForARowThatIsGone_IsNotAnError()
    {
        await harness.Families.CreateIfNotExistsAsync();

        await harness.ChildRepository().MarkDeletingAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
    }

    [Fact]
    public async Task RemoveAsync_RemovesOnlyThatRow_AndIsRepeatable()
    {
        var family = NewFamily();
        var gone = NewChild(family, 0);
        var sibling = NewChild(family, 1);
        await harness.FamilyRepository().RegisterAsync(NewUid(), family, NewParent(family), [gone, sibling], CancellationToken.None);

        await harness.ChildRepository().RemoveAsync(family.FamilyId, gone.ChildId, CancellationToken.None);
        await harness.ChildRepository().RemoveAsync(family.FamilyId, gone.ChildId, CancellationToken.None);

        Assert.Equal(
            [sibling.ChildId],
            (await harness.ChildRepository().CollectionAsync(family.FamilyId, CancellationToken.None)).Select(child => child.ChildId)
        );
        Assert.True((await harness.Families.GetEntityIfExistsAsync<TableEntity>(family.FamilyId.ToString("D"), "family")).HasValue);
    }

    [Fact]
    public async Task SingleAsync_ChildOfAnotherFamily_IsNotFound()
    {
        var mine = await RegisteredAsync(1);
        var theirs = NewFamily();
        var theirChild = NewChild(theirs, 0);
        await harness.FamilyRepository().RegisterAsync(NewUid(), theirs, NewParent(theirs), [theirChild], CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            harness.ChildRepository().SingleAsync(mine.FamilyId, theirChild.ChildId, CancellationToken.None)
        );
    }

    private async Task<Family> RegisteredAsync(int children)
    {
        var family = NewFamily();
        await harness.FamilyRepository().RegisterAsync(
            NewUid(),
            family,
            NewParent(family),
            [.. Enumerable.Range(0, children).Select(position => NewChild(family, position))],
            CancellationToken.None
        );

        return family;
    }

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    public void Dispose() => harness.Dispose();
}
