using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Lantern.Functions.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lantern.Functions.Tests;

// Registering or adding a Child sends a create-workspace action; the Functions handler makes the Class folder and copies the
// Child's Content slice (ADR-0013). These tests run the real API and the real handler on Azurite, over the Board masters that
// ApiBoardMasters seeds, and compare each Child's folder with a slice written out by hand.
[Collection(FunctionsCollection.Name)]
public sealed class ContentSliceTests(AzuriteFixture azurite) : IDisposable
{
    private readonly FunctionsHost host = new(azurite.ConnectionString);

    private BlobContainerClient Family => azurite.CreateBlobClient().GetBlobContainerClient(TestSettings.WorkspaceContainer);

    private TableClient Actions => azurite.CreateClient().GetTableClient("actions");

    [Fact]
    public async Task Register_SscFamilyWithThreeChildren_GivesEachChildItsOwnSliceForTheirClass()
    {
        using var client = host.Caller(NewUid());

        // Two Children share Class 3: each gets their own copy, and Class 1's Child gets only Class 1's Books.
        var family = await client.RegisterAsync(BoardType.Ssc, 1, 3, 3);
        await host.DeliverAsync();

        foreach (var child in family.Children)
        {
            Assert.Equal(ApiBoardMasters.SscSlice(child.ClassLevel), await SliceAsync(family.FamilyId, child.ChildId, child.ClassLevel));
        }

        Assert.Equal(3, family.Children.Select(child => child.ChildId).Distinct().Count());
    }

    [Fact]
    public async Task Register_CbseFamily_GivesEachChildTheNcertSliceOfTheirClass()
    {
        using var client = host.Caller(NewUid());

        var family = await client.RegisterAsync(BoardType.Cbse, 2, 9, 10);
        await host.DeliverAsync();

        foreach (var child in family.Children)
        {
            Assert.Equal(ApiBoardMasters.NcertSlice(child.ClassLevel), await SliceAsync(family.FamilyId, child.ChildId, child.ClassLevel));
        }
    }

    [Fact]
    public async Task Register_SixChildren_EachGetTheirSlice_AndTheLedgerIsEmptyAfterwards()
    {
        using var client = host.Caller(NewUid());

        var family = await client.RegisterAsync(BoardType.Ssc, 1, 2, 3, 4, 5, 5);
        await host.DeliverAsync();

        foreach (var child in family.Children)
        {
            Assert.Equal(ApiBoardMasters.SscSlice(child.ClassLevel), await SliceAsync(family.FamilyId, child.ChildId, child.ClassLevel));
        }

        Assert.Empty(await PendingAsync(family.FamilyId, family.Children));
    }

    [Fact]
    public async Task AddChild_GivesTheNewChildItsSlice_AndLeavesTheFirstChildsAlone()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Ssc, 3);
        await host.DeliverAsync();
        var first = family.Children[0];
        var before = await SliceEtagsAsync(family.FamilyId, first.ChildId, first.ClassLevel);
        var added = Guid.NewGuid();

        await client.Children.AddAsync(NewChild(added, 4), CancellationToken.None);
        await host.DeliverAsync();

        Assert.Equal(ApiBoardMasters.SscSlice(4), await SliceAsync(family.FamilyId, added, 4));
        Assert.Equal(before, await SliceEtagsAsync(family.FamilyId, first.ChildId, first.ClassLevel));
    }

    [Fact]
    public async Task AddChild_AddedTwice_StillGetsOneSlice()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 5);
        await host.DeliverAsync();
        var added = Guid.NewGuid();

        await client.Children.AddAsync(NewChild(added, 6), CancellationToken.None);
        await client.Children.AddAsync(NewChild(added, 6), CancellationToken.None);
        await host.DeliverAsync();

        Assert.Equal(ApiBoardMasters.NcertSlice(6), await SliceAsync(family.FamilyId, added, 6));
        Assert.Single(host.Sender.Sent, m => m.Type == ActionType.CreateWorkspace && m.Payload.Contains(added.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AChildDeletedBeforeTheCopyRuns_GetsNoSlice_AndNoActionIsLeftPending()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Ssc, 3);
        await host.DeliverAsync();
        var added = Guid.NewGuid();
        await client.Children.AddAsync(NewChild(added, 3), CancellationToken.None);

        await client.Children.RemoveAsync(added, CancellationToken.None);
        await host.DeliverAsync();

        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{added:D}/"));
        Assert.Empty(await PendingAsync(family.FamilyId, [new Child { ChildId = added, ClassLevel = 3 }]));
    }

    [Fact]
    public async Task DeletingAChild_ErasesItsSlice_AndKeepsItsSiblings()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Ssc, 3, 3);
        await host.DeliverAsync();
        var (gone, kept) = (family.Children[0], family.Children[1]);

        await client.Children.RemoveAsync(gone.ChildId, CancellationToken.None);
        await host.DeliverAsync();

        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/{gone.ChildId:D}/"));
        Assert.Equal(ApiBoardMasters.SscSlice(3), await SliceAsync(family.FamilyId, kept.ChildId, 3));
    }

    [Fact]
    public async Task DeletingTheFamily_ErasesEveryChildsSlice()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 1, 4, 4);
        await host.DeliverAsync();

        await client.EraseFamilyAsync();
        await host.DeliverAsync();

        Assert.Empty(await NamesAsync($"{family.FamilyId:D}/"));
    }

    [Fact]
    public async Task WhenTheCopyFails_TheActionStaysPending_AndTheNextDeliveryFinishesIt()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Ssc, 3);
        var failing = new Mock<IWorkspaceStore>();
        failing
            .Setup(store => store.CopyContentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<BoardType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("storage down"));
        using var broken = new FunctionsHost(azurite.ConnectionString, configure: services => services.AddSingleton(failing.Object));

        await Assert.ThrowsAsync<IOException>(() =>
            broken.Services.DeliverAsync(host.Sender.Sent.Single(m => m.Type == ActionType.CreateWorkspace), CancellationToken.None)
        );

        Assert.NotEmpty(await PendingAsync(family.FamilyId, family.Children));

        await host.DeliverAsync();

        Assert.Equal(ApiBoardMasters.SscSlice(3), await SliceAsync(family.FamilyId, family.Children[0].ChildId, 3));
        Assert.Empty(await PendingAsync(family.FamilyId, family.Children));
    }

    [Fact]
    public async Task TheSameMessageDeliveredAtTheSameTime_ProducesOneExactSlice_AndClearsTheLedger()
    {
        using var client = host.Caller(NewUid());
        var family = await client.RegisterAsync(BoardType.Cbse, 7);
        var message = host.Sender.Sent.Single(m => m.Type == ActionType.CreateWorkspace);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => host.Services.DeliverAsync(message, CancellationToken.None)));

        Assert.Equal(ApiBoardMasters.NcertSlice(7), await SliceAsync(family.FamilyId, family.Children[0].ChildId, 7));
        Assert.Empty(await PendingAsync(family.FamilyId, family.Children));
        var action = new CreateWorkspacePayload(family.FamilyId, family.Children[0].ChildId, 7).ActionId();
        Assert.Equal("create-workspace", StorageProbe.DoneBy(Assert.Single(await Actions.QueryAsync<TableEntity>(row => row.PartitionKey == nameof(ActionType.CreateWorkspace) && row.RowKey == action).ToListAsync())));
    }

    [Fact]
    public async Task ManyFamiliesRegisteringTogether_EachChildGetsItsOwnExactSlice()
    {
        // Twelve Families of three Children on both Boards, registered at the same moment and handled by concurrent workers.
        var registrations = await Task.WhenAll(
            Enumerable
                .Range(0, 12)
                .Select(async index =>
                {
                    var board = index % 2 == 0 ? BoardType.Ssc : BoardType.Cbse;
                    using var client = host.Caller(NewUid());

                    return (Board: board, Family: await client.RegisterAsync(board, 1 + (index % 5), 2 + (index % 3), 3));
                })
        );

        await Parallel.ForEachAsync(
            host.Sender.Sent.Where(m => m.Type == ActionType.CreateWorkspace),
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (message, token) => await host.Services.DeliverAsync(message, token)
        );

        Assert.Equal(36, host.Sender.Sent.Count(m => m.Type == ActionType.CreateWorkspace));
        foreach (var (board, family) in registrations)
        {
            foreach (var child in family.Children)
            {
                var expected = board == BoardType.Ssc ? ApiBoardMasters.SscSlice(child.ClassLevel) : ApiBoardMasters.NcertSlice(child.ClassLevel);
                Assert.Equal(expected, await SliceAsync(family.FamilyId, child.ChildId, child.ClassLevel));
            }

            Assert.Empty(await PendingAsync(family.FamilyId, family.Children));
        }
    }

    public void Dispose() => host.Dispose();

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Child NewChild(Guid id, int classLevel) =>
        new() { ChildId = id, Name = Locked("Kavya"), ClassLevel = classLevel, BirthYear = Locked("2018"), School = Locked("Green School") };

    private static string Locked(string plaintext) => $"v1.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext))}.{Guid.NewGuid():N}";

    // The names under the Child's content folder, with the prefix removed and sorted, to compare with a slice written out by hand.
    private async Task<string[]> SliceAsync(Guid familyId, Guid childId, int classLevel)
    {
        var prefix = $"{familyId:D}/{childId:D}/{classLevel}/content/";

        return [.. (await NamesAsync(prefix)).Select(name => name[prefix.Length..]).Order(StringComparer.Ordinal)];
    }

    private async Task<Dictionary<string, string>> SliceEtagsAsync(Guid familyId, Guid childId, int classLevel)
    {
        var etags = new Dictionary<string, string>();
        await foreach (var blob in Family.GetBlobsAsync(BlobTraits.None, BlobStates.None, $"{familyId:D}/{childId:D}/{classLevel}/content/", CancellationToken.None))
        {
            etags[blob.Name] = blob.Properties.ETag?.ToString();
        }

        return etags;
    }

    private async Task<List<string>> NamesAsync(string prefix)
    {
        var names = new List<string>();
        try
        {
            await foreach (var blob in Family.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
            {
                names.Add(blob.Name);
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            // Nothing was written.
        }

        return names;
    }

    private async Task<List<TableEntity>> PendingAsync(Guid familyId, IEnumerable<Child> children)
    {
        var rows = new List<TableEntity>();
        foreach (var child in children)
        {
            var id = new CreateWorkspacePayload(familyId, child.ChildId, child.ClassLevel).ActionId();
            rows.AddRange((await Actions.QueryAsync<TableEntity>(row => row.PartitionKey == nameof(ActionType.CreateWorkspace) && row.RowKey == id).ToListAsync()).Where(StorageProbe.IsUnfinished));
        }

        return rows;
    }
}
