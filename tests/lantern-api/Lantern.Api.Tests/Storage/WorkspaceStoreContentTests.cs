using System.Text.Json.Nodes;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Repository;

namespace Lantern.Api.Tests.Storage;

[Collection(ApiCollection.Name)]
// Real Azurite, and masters shaped like UAT's ncert and ssc containers (BoardMaster). The expected lists below are written
// out by hand, not derived from the rules the store applies, so a test cannot agree with a wrong rule.
public sealed class WorkspaceStoreContentTests(AzuriteFixture azurite)
{
    private const int ClassThree = 3;

    // Class 3's built Books are cxyz1 and cmat1. The unbuilt Hindi and Urdu entries, the Class 4 Books (cxyz10 shares
    // cxyz1's prefix), every build leftover and the strays NCERT never has are all absent.
    private static readonly string[] NcertClassThree =
    [
        "catalog.json",
        "refined/cmat1/cmat101.json",
        "refined/cxyz1/cxyz101.json",
        "refined/cxyz1/cxyz102.json",
        "summaries/cmat1/cmat101.json",
        "summaries/cxyz1/cxyz101.json",
        "summaries/cxyz1/cxyz102.json",
        "version.json",
    ];

    // SSC adds board.json and a toc and guidance file per Book. ssc3-evs12 (Class 4) shares ssc3-evs1's prefix.
    private static readonly string[] SscClassThree =
    [
        "board.json",
        "catalog.json",
        "guidance/ssc3-english.json",
        "guidance/ssc3-evs1.json",
        "guidance/ssc3-maths.json",
        "refined/ssc3-english/ssc3-english-01.json",
        "refined/ssc3-evs1/ssc3-evs1-01.json",
        "refined/ssc3-maths/ssc3-maths-01.json",
        "refined/ssc3-maths/ssc3-maths-02.json",
        "summaries/ssc3-english/ssc3-english-01.json",
        "summaries/ssc3-evs1/ssc3-evs1-01.json",
        "summaries/ssc3-maths/ssc3-maths-01.json",
        "summaries/ssc3-maths/ssc3-maths-02.json",
        "toc/ssc3-english.json",
        "toc/ssc3-evs1.json",
        "toc/ssc3-maths.json",
        "version.json",
    ];

    private readonly BlobContainerClient family = azurite.CreateBlobClient().GetBlobContainerClient($"family{Guid.NewGuid():N}");

    [Fact]
    public async Task CopyContentAsync_Ncert_CopiesExactlyTheBuiltBooksOfTheClass_AndNothingElse()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());

        await CopyAsync(ncert, null, familyId, childId, ClassThree, BoardType.Cbse);

        Assert.Equal(NcertClassThree, await ContentNamesAsync(familyId, childId, ClassThree));
        await AssertBytesMatchAsync(ncert, familyId, childId, ClassThree, NcertClassThree);
    }

    [Fact]
    public async Task CopyContentAsync_Ssc_CopiesExactlyTheBooksOfTheClassWithTheirTocAndGuidance_AndNothingElse()
    {
        var ssc = await BoardMaster.SscAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());

        await CopyAsync(null, ssc, familyId, childId, ClassThree, BoardType.Ssc);

        Assert.Equal(SscClassThree, await ContentNamesAsync(familyId, childId, ClassThree));
        await AssertBytesMatchAsync(ssc, familyId, childId, ClassThree, SscClassThree);
    }

    [Fact]
    public async Task CopyContentAsync_Ncert_TheCatalogHoldsOnlyTheCopiedBooks_EntriesUnchanged()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());

        await CopyAsync(ncert, null, familyId, childId, ClassThree, BoardType.Cbse);

        // Master order, every field kept (future_field is not one the code knows), nothing for the unbuilt Hindi or Urdu Books.
        var expected = JsonNode.Parse(
            """
            [
              {"book_id":"cxyz1","grade":3,"subject":"English","title":"Test Reader","medium":"English","first_chapter":1,"last_chapter":2,"has_front_matter":true,"future_field":{"keep":["me",1]}},
              {"book_id":"cmat1","grade":3,"subject":"Mathematics","title":"Test Maths","medium":"English","first_chapter":1,"last_chapter":2,"has_front_matter":false}
            ]
            """
        );
        Assert.True(JsonNode.DeepEquals(expected, await ContentJsonAsync(familyId, childId, ClassThree, "catalog.json")));
    }

    [Fact]
    public async Task CopyContentAsync_Ssc_TheCatalogHoldsOnlyTheClassesBooks_EntriesUnchanged()
    {
        var ssc = await BoardMaster.SscAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());

        await CopyAsync(null, ssc, familyId, childId, ClassThree, BoardType.Ssc);

        var catalog = (JsonArray)await ContentJsonAsync(familyId, childId, ClassThree, "catalog.json");
        Assert.Equal(["ssc3-maths", "ssc3-english", "ssc3-evs1"], catalog.Select(entry => (string)entry["book_id"]!));
        Assert.All(catalog, entry => Assert.Equal(3, (int)entry["grade"]!));
        Assert.Equal("aa", (string)catalog[0]["source_sha256"]!);
        Assert.Null(catalog[0]["part"]);
    }

    [Fact]
    public async Task CopyContentAsync_WritesAVersionThatNamesTheBoardClassBooksAndFileCount()
    {
        var ssc = await BoardMaster.SscAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());

        await CopyAsync(null, ssc, familyId, childId, ClassThree, BoardType.Ssc);

        var version = await ContentJsonAsync(familyId, childId, ClassThree, "version.json");
        Assert.Equal("ssc", (string)version["board"]!);
        Assert.Equal(3, (int)version["class"]!);
        Assert.Equal(["ssc3-maths", "ssc3-english", "ssc3-evs1"], ((JsonArray)version["books"]!).Select(book => (string)book!));
        // Every blob but version.json itself.
        Assert.Equal(SscClassThree.Length - 1, (int)version["files"]!);
        Assert.True(DateTimeOffset.TryParse((string)version["copiedAt"]!, out var copiedAt) && copiedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task CopyContentAsync_ABookDeclaringChaptersThatAreNotBuilt_CopiesWhatIsBuilt()
    {
        // NCERT's jewe2 declares nine Chapters and has seven built, so the catalog's range cannot be the check.
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());

        await CopyAsync(ncert, null, familyId, childId, ClassThree, BoardType.Cbse);

        var names = await ContentNamesAsync(familyId, childId, ClassThree);
        Assert.Contains("refined/cmat1/cmat101.json", names);
        Assert.DoesNotContain("refined/cmat1/cmat102.json", names);
    }

    [Theory]
    [InlineData(BoardType.Ssc, "board.json")]
    [InlineData(BoardType.Ssc, "toc/ssc3-maths.json")]
    [InlineData(BoardType.Ssc, "guidance/ssc3-evs1.json")]
    [InlineData(BoardType.Ssc, "summaries/ssc3-maths/ssc3-maths-02.json")]
    [InlineData(BoardType.Ssc, "refined/ssc3-english/ssc3-english-01.json")]
    [InlineData(BoardType.Cbse, "summaries/cxyz1/cxyz102.json")]
    [InlineData(BoardType.Cbse, "summaries/cmat1/cmat101.json")]
    public async Task CopyContentAsync_ARequiredFileIsMissing_Throws_AndLeavesNothingBehind(BoardType board, string missing)
    {
        var master = board == BoardType.Ssc ? await BoardMaster.SscAsync(azurite) : await BoardMaster.NcertAsync(azurite);
        await master.RemoveAsync(missing);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CopyAsync(board == BoardType.Cbse ? master : null, board == BoardType.Ssc ? master : null, familyId, childId, ClassThree, board)
        );

        Assert.Empty(await ContentNamesAsync(familyId, childId, ClassThree));
    }

    [Fact]
    public async Task CopyContentAsync_NcertHasNoBuiltBookForTheClass_Throws_AndMarksNothingReady()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());

        // Class 9 has no catalog entry; Class 3's Hindi and Urdu entries are listed but not built.
        await Assert.ThrowsAsync<InvalidOperationException>(() => CopyAsync(ncert, null, familyId, childId, 9, BoardType.Cbse));
        Assert.Empty(await ContentNamesAsync(familyId, childId, 9));

        await ncert.RemoveAsync("refined/cxyz1/cxyz101.json");
        await ncert.RemoveAsync("refined/cxyz1/cxyz102.json");
        await ncert.RemoveAsync("refined/cmat1/cmat101.json");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CopyAsync(ncert, null, familyId, childId, ClassThree, BoardType.Cbse));
        Assert.Empty(await ContentNamesAsync(familyId, childId, ClassThree));
    }

    [Fact]
    public async Task CopyContentAsync_SscListsABookItHasNotBuilt_Throws()
    {
        // SSC lists only what it built, so a listed Book with no refined content is a broken master, not an unbuilt medium.
        var ssc = await BoardMaster.SscAsync(azurite);
        foreach (var name in new[] { "refined/ssc3-evs1/ssc3-evs1-01.json", "summaries/ssc3-evs1/ssc3-evs1-01.json" })
        {
            await ssc.RemoveAsync(name);
        }

        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => CopyAsync(null, ssc, familyId, childId, ClassThree, BoardType.Ssc));
        Assert.Empty(await ContentNamesAsync(familyId, childId, ClassThree));
    }

    [Fact]
    public async Task CopyContentAsync_TheMasterHasNoCatalog_Throws()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        await ncert.RemoveAsync("catalog.json");

        await Assert.ThrowsAsync<RequestFailedException>(() =>
            CopyAsync(ncert, null, Guid.NewGuid(), Guid.NewGuid(), ClassThree, BoardType.Cbse)
        );
    }

    [Fact]
    public async Task CopyContentAsync_ABookIdThatIsAPath_IsRefused()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        await ncert.PutAsync("catalog.json", """[{"book_id":"../text/cxyz1","grade":3}]""");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CopyAsync(ncert, null, Guid.NewGuid(), Guid.NewGuid(), ClassThree, BoardType.Cbse)
        );
    }

    [Fact]
    public async Task CopyContentAsync_ASliceThatIsReady_IsLeftAloneEvenIfTheMasterHasChanged()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        await CopyAsync(ncert, null, familyId, childId, ClassThree, BoardType.Cbse);
        var before = await ContentEtagsAsync(familyId, childId, ClassThree);
        var pinned = await ContentBytesAsync(familyId, childId, ClassThree, "refined/cxyz1/cxyz101.json");

        await ncert.PutAsync("refined/cxyz1/cxyz101.json", "the master was corrected after this Child was copied");
        await CopyAsync(ncert, null, familyId, childId, ClassThree, BoardType.Cbse);

        Assert.Equal(before, await ContentEtagsAsync(familyId, childId, ClassThree));
        Assert.Equal(pinned, await ContentBytesAsync(familyId, childId, ClassThree, "refined/cxyz1/cxyz101.json"));
    }

    [Fact]
    public async Task CopyContentAsync_AHalfMadeSlice_IsMadeAgainFromTheStart_AndLosesWhatIsNotWanted()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        var prefix = $"{familyId:D}/{childId:D}/{ClassThree}/content/";
        await family.CreateIfNotExistsAsync();
        // What an earlier attempt left: one wanted file with old bytes, one file the master no longer has, and no version.json.
        await family.GetBlobClient(prefix + "refined/cxyz1/cxyz101.json").UploadAsync(BinaryData.FromString("half"), overwrite: true);
        await family.GetBlobClient(prefix + "refined/cxyz1/cxyz199.json").UploadAsync(BinaryData.FromString("dropped"), overwrite: true);

        await CopyAsync(ncert, null, familyId, childId, ClassThree, BoardType.Cbse);

        Assert.Equal(NcertClassThree, await ContentNamesAsync(familyId, childId, ClassThree));
        await AssertBytesMatchAsync(ncert, familyId, childId, ClassThree, NcertClassThree);
    }

    [Fact]
    public async Task CopyContentAsync_WhenAFileCannotBeReadMidCopy_LeavesNoVersion_AndTheRetryMakesTheExactSet()
    {
        var ssc = await BoardMaster.SscAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        var outage = new FailingReads("ssc3-maths-02.json");
        var flaky = new BlobServiceClient(azurite.ConnectionString, OptionsWith(outage)).GetBlobContainerClient(ssc.Container.Name);

        var store = StoreOver(null, flaky);
        await store.CreateAsync(familyId, childId, ClassThree, CancellationToken.None);
        await Assert.ThrowsAnyAsync<Exception>(() => store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Ssc, CancellationToken.None));

        Assert.DoesNotContain("version.json", await ContentNamesAsync(familyId, childId, ClassThree));

        outage.Recover();
        await store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Ssc, CancellationToken.None);

        Assert.Equal(SscClassThree, await ContentNamesAsync(familyId, childId, ClassThree));
        await AssertBytesMatchAsync(ssc, familyId, childId, ClassThree, SscClassThree);
    }

    [Fact]
    public async Task CopyContentAsync_AFileIsCorrectedInTheMasterMidCopy_IsNotMarkedReady_AndTheRetryHasTheNewBytes()
    {
        var ssc = await BoardMaster.SscAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        var edit = new OnFirstRead("ssc3-maths-01.json", () => ssc.PutAsync("refined/ssc3-maths/ssc3-maths-01.json", "corrected during the copy"));
        var store = StoreOver(null, new BlobServiceClient(azurite.ConnectionString, OptionsWith(edit)).GetBlobContainerClient(ssc.Container.Name));
        await store.CreateAsync(familyId, childId, ClassThree, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Ssc, CancellationToken.None));
        Assert.DoesNotContain("version.json", await ContentNamesAsync(familyId, childId, ClassThree));

        await store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Ssc, CancellationToken.None);

        Assert.Equal(SscClassThree, await ContentNamesAsync(familyId, childId, ClassThree));
        Assert.Equal("corrected during the copy", System.Text.Encoding.UTF8.GetString(await ContentBytesAsync(familyId, childId, ClassThree, "refined/ssc3-maths/ssc3-maths-01.json")));
        await AssertBytesMatchAsync(ssc, familyId, childId, ClassThree, SscClassThree);
    }

    [Fact]
    public async Task CopyContentAsync_AChapterIsAddedToTheMasterMidCopy_IsNotMarkedReady_AndTheRetryHasIt()
    {
        var ssc = await BoardMaster.SscAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        var upload = new OnFirstRead(
            "ssc3-maths-01.json",
            async () =>
            {
                await ssc.PutAsync("refined/ssc3-maths/ssc3-maths-03.json");
                await ssc.PutAsync("summaries/ssc3-maths/ssc3-maths-03.json");
            }
        );
        var store = StoreOver(null, new BlobServiceClient(azurite.ConnectionString, OptionsWith(upload)).GetBlobContainerClient(ssc.Container.Name));
        await store.CreateAsync(familyId, childId, ClassThree, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Ssc, CancellationToken.None));
        Assert.DoesNotContain("version.json", await ContentNamesAsync(familyId, childId, ClassThree));

        await store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Ssc, CancellationToken.None);

        var names = await ContentNamesAsync(familyId, childId, ClassThree);
        Assert.Contains("refined/ssc3-maths/ssc3-maths-03.json", names);
        Assert.Contains("summaries/ssc3-maths/ssc3-maths-03.json", names);
        Assert.Equal(SscClassThree.Length + 2, names.Length);
    }

    [Fact]
    public async Task CopyContentAsync_AChapterIsRemovedFromTheMasterMidCopy_IsNotMarkedReady_AndTheRetryLeavesItOut()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        var remove = new OnFirstRead(
            "cxyz101.json",
            async () =>
            {
                await ncert.RemoveAsync("refined/cxyz1/cxyz102.json");
                await ncert.RemoveAsync("summaries/cxyz1/cxyz102.json");
            }
        );
        var store = StoreOver(new BlobServiceClient(azurite.ConnectionString, OptionsWith(remove)).GetBlobContainerClient(ncert.Container.Name), null);
        await store.CreateAsync(familyId, childId, ClassThree, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Cbse, CancellationToken.None));
        Assert.DoesNotContain("version.json", await ContentNamesAsync(familyId, childId, ClassThree));

        await store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Cbse, CancellationToken.None);

        string[] expected = [.. NcertClassThree.Where(name => !name.EndsWith("cxyz102.json", StringComparison.Ordinal))];
        Assert.Equal(expected, await ContentNamesAsync(familyId, childId, ClassThree));
    }

    [Fact]
    public async Task CopyContentAsync_ManyChildrenOfBothBoardsAtOnce_EachGetTheirOwnExactSlice()
    {
        // Many Families registering in the same minute: 30 Children across 6 Families on both Boards, all copying together.
        var ncert = await BoardMaster.NcertAsync(azurite);
        var ssc = await BoardMaster.SscAsync(azurite);
        var store = Store(ncert, ssc);
        var children = Enumerable
            .Range(0, 30)
            .Select(index => (FamilyId: Guid.NewGuid(), ChildId: Guid.NewGuid(), Board: index % 2 == 0 ? BoardType.Cbse : BoardType.Ssc))
            .ToArray();

        await Task.WhenAll(
            children.Select(async child =>
            {
                await store.CreateAsync(child.FamilyId, child.ChildId, ClassThree, CancellationToken.None);
                await store.CopyContentAsync(child.FamilyId, child.ChildId, ClassThree, child.Board, CancellationToken.None);
            })
        );

        foreach (var child in children)
        {
            var (expected, master) = child.Board == BoardType.Cbse ? (NcertClassThree, ncert) : (SscClassThree, ssc);
            Assert.Equal(expected, await ContentNamesAsync(child.FamilyId, child.ChildId, ClassThree));
            await AssertBytesMatchAsync(master, child.FamilyId, child.ChildId, ClassThree, expected);
        }
    }

    [Fact]
    public async Task CopyContentAsync_ACopyBesideOneThatFinished_NeverDeletesTheVersionTheOtherWrote()
    {
        // Copy A is under way when copy B (the same message delivered twice) finishes and is ready. The master then changes, so A
        // gives up; B's version.json must still be there, since A's leftover clean-up runs before A notices.
        var ssc = await BoardMaster.SscAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        var other = Store(null, ssc);
        await other.CreateAsync(familyId, childId, ClassThree, CancellationToken.None);
        var beside = new OnFirstRead(
            "ssc3-maths-01.json",
            async () =>
            {
                await other.CopyContentAsync(familyId, childId, ClassThree, BoardType.Ssc, CancellationToken.None);
                await ssc.PutAsync("refined/ssc3-maths/ssc3-maths-02.json", "changed after the other copy finished");
            }
        );
        var store = StoreOver(null, new BlobServiceClient(azurite.ConnectionString, OptionsWith(beside)).GetBlobContainerClient(ssc.Container.Name));

        await Assert.ThrowsAnyAsync<Exception>(() => store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Ssc, CancellationToken.None));

        Assert.Equal(SscClassThree, await ContentNamesAsync(familyId, childId, ClassThree));
    }

    [Fact]
    public async Task CopyContentAsync_TheMasterIsNotChangedByACopy()
    {
        var ssc = await BoardMaster.SscAsync(azurite);
        var before = await ssc.SnapshotAsync();

        await CopyAsync(null, ssc, Guid.NewGuid(), Guid.NewGuid(), ClassThree, BoardType.Ssc);

        Assert.Equal(before, await ssc.SnapshotAsync());
    }

    [Fact]
    public async Task CopyContentAsync_TouchesNothingOutsideTheClassContent()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, childId, sibling) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await family.CreateIfNotExistsAsync();
        string[] neighbours =
        [
            $"{familyId:D}/{childId:D}/{ClassThree}/history/entry.json",
            $"{familyId:D}/{childId:D}/4/content/refined/old.json",
            $"{familyId:D}/{sibling:D}/{ClassThree}/content/refined/theirs.json",
            $"{familyId:D}/{sibling:D}/{ClassThree}/class.json",
        ];
        foreach (var neighbour in neighbours)
        {
            await family.GetBlobClient(neighbour).UploadAsync(BinaryData.FromString(neighbour), overwrite: true);
        }

        await CopyAsync(ncert, null, familyId, childId, ClassThree, BoardType.Cbse);

        foreach (var neighbour in neighbours)
        {
            Assert.Equal(neighbour, (await family.GetBlobClient(neighbour).DownloadContentAsync()).Value.Content.ToString());
        }

        Assert.True(await family.GetBlobClient($"{familyId:D}/{childId:D}/{ClassThree}/class.json").ExistsAsync());
    }

    [Fact]
    public async Task CopyContentAsync_TwoChildrenInTheSameClass_EachGetTheirOwnExactCopy_AndRemovingOneKeepsTheOther()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, first, second) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await CopyAsync(ncert, null, familyId, first, ClassThree, BoardType.Cbse);
        await CopyAsync(ncert, null, familyId, second, ClassThree, BoardType.Cbse);

        Assert.Equal(NcertClassThree, await ContentNamesAsync(familyId, first, ClassThree));
        Assert.Equal(NcertClassThree, await ContentNamesAsync(familyId, second, ClassThree));

        await Store(ncert, null).RemoveAsync(familyId, first, CancellationToken.None);

        Assert.Empty(await ContentNamesAsync(familyId, first, ClassThree));
        Assert.Equal(NcertClassThree, await ContentNamesAsync(familyId, second, ClassThree));
    }

    [Fact]
    public async Task CopyContentAsync_ChildrenInDifferentClassesOfOneBoard_EachGetOnlyTheirClass()
    {
        var ssc = await BoardMaster.SscAsync(azurite);
        var (familyId, three, four) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await CopyAsync(null, ssc, familyId, three, 3, BoardType.Ssc);
        await CopyAsync(null, ssc, familyId, four, 4, BoardType.Ssc);

        Assert.Equal(SscClassThree, await ContentNamesAsync(familyId, three, 3));
        // Class 4 holds ssc3-evs12 and ssc4-maths: one Book each in the catalog, so the slice is small and distinct.
        string[] classFour =
        [
            "board.json",
            "catalog.json",
            "guidance/ssc3-evs12.json",
            "guidance/ssc4-maths.json",
            "refined/ssc3-evs12/ssc3-evs12-01.json",
            "refined/ssc4-maths/ssc4-maths-01.json",
            "summaries/ssc3-evs12/ssc3-evs12-01.json",
            "summaries/ssc4-maths/ssc4-maths-01.json",
            "toc/ssc3-evs12.json",
            "toc/ssc4-maths.json",
            "version.json",
        ];
        Assert.Equal(classFour, await ContentNamesAsync(familyId, four, 4));
    }

    [Fact]
    public async Task CopyContentAsync_TheSameCopyTwiceAtOnce_FinishesWithTheExactSet()
    {
        var ssc = await BoardMaster.SscAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        var store = Store(null, ssc);
        await store.CreateAsync(familyId, childId, ClassThree, CancellationToken.None);

        await Task.WhenAll(
            Enumerable
                .Range(0, 4)
                .Select(_ => store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Ssc, CancellationToken.None))
        );

        Assert.Equal(SscClassThree, await ContentNamesAsync(familyId, childId, ClassThree));
        await AssertBytesMatchAsync(ssc, familyId, childId, ClassThree, SscClassThree);
    }

    [Fact]
    public async Task CopyContentAsync_ABoardWithNoMasterContainer_Throws()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, childId) = (Guid.NewGuid(), Guid.NewGuid());
        var store = Store(ncert, null);
        await store.CreateAsync(familyId, childId, ClassThree, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CopyContentAsync(familyId, childId, ClassThree, BoardType.Ssc, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveFamilyAsync_ErasesEveryChildsContentWithTheRest()
    {
        var ncert = await BoardMaster.NcertAsync(azurite);
        var (familyId, first, second) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await CopyAsync(ncert, null, familyId, first, ClassThree, BoardType.Cbse);
        await CopyAsync(ncert, null, familyId, second, ClassThree, BoardType.Cbse);

        await Store(ncert, null).RemoveFamilyAsync(familyId, CancellationToken.None);

        Assert.Empty(await ContentNamesAsync(familyId, first, ClassThree));
        Assert.Empty(await ContentNamesAsync(familyId, second, ClassThree));
    }

    // The handler makes the Class folder first, then the slice.
    private async Task CopyAsync(BoardMaster ncert, BoardMaster ssc, Guid familyId, Guid childId, int classLevel, BoardType board)
    {
        var store = Store(ncert, ssc);
        await store.CreateAsync(familyId, childId, classLevel, CancellationToken.None);
        await store.CopyContentAsync(familyId, childId, classLevel, board, CancellationToken.None);
    }

    private WorkspaceStore Store(BoardMaster ncert, BoardMaster ssc) => StoreOver(ncert?.Container, ssc?.Container);

    private WorkspaceStore StoreOver(BlobContainerClient ncert, BlobContainerClient ssc)
    {
        var masters = new Dictionary<BoardType, BlobContainerClient>();
        if (ncert is not null)
        {
            masters[BoardType.Cbse] = ncert;
        }

        if (ssc is not null)
        {
            masters[BoardType.Ssc] = ssc;
        }

        return new WorkspaceStore(family, masters, TimeProvider.System, createContainer: true);
    }

    private static BlobClientOptions OptionsWith(HttpPipelinePolicy policy)
    {
        var options = new BlobClientOptions();
        options.Retry.MaxRetries = 0;
        options.AddPolicy(policy, HttpPipelinePosition.PerCall);

        return options;
    }

    private static string ContentPrefix(Guid familyId, Guid childId, int classLevel) => $"{familyId:D}/{childId:D}/{classLevel}/content/";

    private async Task<string[]> ContentNamesAsync(Guid familyId, Guid childId, int classLevel)
    {
        var prefix = ContentPrefix(familyId, childId, classLevel);
        var names = new List<string>();
        try
        {
            await foreach (var blob in family.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
            {
                names.Add(blob.Name[prefix.Length..]);
            }
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            // Nothing was ever written.
        }

        names.Sort(StringComparer.Ordinal);

        return [.. names];
    }

    private async Task<Dictionary<string, string>> ContentEtagsAsync(Guid familyId, Guid childId, int classLevel)
    {
        var prefix = ContentPrefix(familyId, childId, classLevel);
        var etags = new Dictionary<string, string>();
        await foreach (var blob in family.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
        {
            etags[blob.Name] = blob.Properties.ETag?.ToString();
        }

        return etags;
    }

    private async Task<byte[]> ContentBytesAsync(Guid familyId, Guid childId, int classLevel, string name) =>
        (await family.GetBlobClient(ContentPrefix(familyId, childId, classLevel) + name).DownloadContentAsync()).Value.Content.ToArray();

    private async Task<JsonNode> ContentJsonAsync(Guid familyId, Guid childId, int classLevel, string name) =>
        JsonNode.Parse(await ContentBytesAsync(familyId, childId, classLevel, name))!;

    // Every file but the two the store writes itself is the master's file, byte for byte.
    private async Task AssertBytesMatchAsync(BoardMaster master, Guid familyId, Guid childId, int classLevel, string[] names)
    {
        foreach (var name in names.Where(name => name is not ("catalog.json" or "version.json")))
        {
            Assert.Equal(await master.BytesAsync(name), await ContentBytesAsync(familyId, childId, classLevel, name));
        }
    }

    // Reads of one file fail with a 503, as a storage outage would, until the test says it is over.
    private sealed class FailingReads(string fileName) : HttpPipelinePolicy
    {
        private volatile bool down = true;

        public void Recover() => down = false;

        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            Check(message);
            ProcessNext(message, pipeline);
        }

        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            Check(message);

            return ProcessNextAsync(message, pipeline);
        }

        private void Check(HttpMessage message)
        {
            if (down && message.Request.Method == RequestMethod.Get && message.Request.Uri.Path.EndsWith(fileName, StringComparison.Ordinal))
            {
                throw new RequestFailedException(503, "blob down");
            }
        }
    }

    // Runs something against the master, once, just before the first read of one file: the maintenance upload landing mid-copy.
    private sealed class OnFirstRead(string fileName, Func<Task> change) : HttpPipelinePolicy
    {
        private int fired;

        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            if (Due(message))
            {
                change().GetAwaiter().GetResult();
            }

            ProcessNext(message, pipeline);
        }

        public override async ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            if (Due(message))
            {
                await change();
            }

            await ProcessNextAsync(message, pipeline);
        }

        private bool Due(HttpMessage message) =>
            message.Request.Method == RequestMethod.Get
            && message.Request.Uri.Path.EndsWith(fileName, StringComparison.Ordinal)
            && Interlocked.Exchange(ref fired, 1) == 0;
    }
}
