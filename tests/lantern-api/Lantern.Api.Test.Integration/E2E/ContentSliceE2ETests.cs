using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lantern.Api.Models;
using Lantern.Core.Constants;

namespace Lantern.Api.Test.Integration.E2E;

// Needs the local Docker stack (deploy/local), which seeds synthetic ncert and ssc containers (LocalBoardMasters), so the
// ids below are those of the stack and not of UAT. Registering a Family sends one create-workspace action per Child; the
// Functions container copies each Child's Content slice from its Board's container (ADR-0013). Each slice is compared with
// a list written out by hand.
[Trait("Category", "E2E")]
public sealed class ContentSliceE2ETests
{
    private static readonly Uri Register = new("/v1/register", UriKind.Relative);
    private static readonly Uri Children = new("/v1/family/children", UriKind.Relative);

    // Azurite's published development account; not a secret.
    private const string AzuriteBlobs =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
        + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
        + "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    [Fact]
    public async Task RegisteringAnSscFamilyWithThreeChildren_GivesEachItsOwnSliceForTheirClass()
    {
        using var client = await NewParentAsync();

        var family = await RegisterAsync(client, BoardType.Ssc, 1, 3, 3);

        foreach (var child in family.Children)
        {
            Assert.Equal(SscSlice(child.ClassLevel), await ReadySliceAsync(family.FamilyId, child.ChildId, child.ClassLevel));
        }
    }

    [Fact]
    public async Task RegisteringACbseFamilyWithThreeChildren_GivesEachTheNcertSliceOfTheirClass()
    {
        using var client = await NewParentAsync();

        var family = await RegisterAsync(client, BoardType.Cbse, 2, 9, 10);

        foreach (var child in family.Children)
        {
            Assert.Equal(NcertSlice(child.ClassLevel), await ReadySliceAsync(family.FamilyId, child.ChildId, child.ClassLevel));
        }
    }

    [Fact]
    public async Task AddingAChild_GivesItASliceToo()
    {
        using var client = await NewParentAsync();
        var family = await RegisterAsync(client, BoardType.Ssc, 2);
        var id = Guid.NewGuid();

        var added = await client.PostAsJsonAsync(
            Children,
            new ChildAddModel { ChildId = id, Name = "locked-kavya", ClassLevel = 4, BirthYear = "locked-year", School = "locked-school" }
        );

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(SscSlice(4), await ReadySliceAsync(family.FamilyId, id, 4));
    }

    [Fact]
    public async Task TheSeededDevFamilysChildren_EachHaveTheirSlice()
    {
        // The seeder registers dev-parent-1's ten CBSE Children, one per Class, and copies each slice itself.
        using var client = await NewParentAsync("dev-parent-1");
        var seeded = (await client.GetFromJsonAsync<FamilyModel>(new Uri("/v1/me", UriKind.Relative)))!;

        foreach (var child in seeded.Children)
        {
            Assert.Equal(NcertSlice(child.ClassLevel), await ReadySliceAsync(seeded.FamilyId, child.ChildId, child.ClassLevel));
        }
    }

    [Fact]
    public async Task TheSeededFamily_HasAnEntryInTheFamilyManifestForEachOfItsTenChildren()
    {
        using var client = await NewParentAsync("dev-parent-1");
        var seeded = (await client.GetFromJsonAsync<FamilyModel>(new Uri("/v1/me", UriKind.Relative)))!;

        var manifest = JsonNode.Parse(await ManifestAsync(seeded.FamilyId))!;

        Assert.Equal("family-manifest-v1", (string)manifest["schema"]!);
        Assert.Equal(10, manifest["children"]!.AsObject().Count);
        foreach (var child in seeded.Children)
        {
            var entry = manifest["children"]![child.ChildId.ToString("D")]!;
            Assert.Equal(child.ClassLevel, (int)entry["class"]!);
            Assert.Equal("cbse-v1", (string)entry["profileVersion"]!);
            Assert.Equal(["mathematics"], entry["subjects"]!.AsArray().Select(subject => (string)subject!["subjectId"]!));
            Assert.Equal(2, (int)entry["subjects"]![0]!["chapterCount"]!);
        }
    }

    private static async Task<string> ManifestAsync(Guid familyId) =>
        (await new BlobServiceClient(AzuriteBlobs).GetBlobContainerClient("family").GetBlobClient($"{familyId:D}/manifest.json").DownloadContentAsync()).Value.Content.ToString();

    // What a Child of this Class gets from the stack's NCERT container: its one built Book, nothing of the unbuilt Hindi Book.
    private static string[] NcertSlice(int classLevel) =>
    [
        "catalog.json",
        "manifest.json",
        $"refined/nbk{classLevel}1/nbk{classLevel}101.json",
        $"refined/nbk{classLevel}1/nbk{classLevel}102.json",
        $"summaries/nbk{classLevel}1/nbk{classLevel}101.json",
        $"summaries/nbk{classLevel}1/nbk{classLevel}102.json",
        "version.json",
    ];

    // The stack's SSC container: board.json, and for each of the Class's two Books a toc, guidance, refined and summaries.
    private static string[] SscSlice(int classLevel) =>
    [
        "board.json",
        "catalog.json",
        $"guidance/sbk{classLevel}-english.json",
        $"guidance/sbk{classLevel}-maths.json",
        "manifest.json",
        $"refined/sbk{classLevel}-english/sbk{classLevel}-english-01.json",
        $"refined/sbk{classLevel}-maths/sbk{classLevel}-maths-01.json",
        $"summaries/sbk{classLevel}-english/sbk{classLevel}-english-01.json",
        $"summaries/sbk{classLevel}-maths/sbk{classLevel}-maths-01.json",
        $"toc/sbk{classLevel}-english.json",
        $"toc/sbk{classLevel}-maths.json",
        "version.json",
    ];

    private static async Task<HttpClient> NewParentAsync(string uid = null)
    {
        var token = await EmulatorTokens.MintAsync(RequireEnv("E2E_AUTH_EMULATOR"), uid ?? $"e2e-{Guid.NewGuid():N}");
        var client = new HttpClient { BaseAddress = new Uri(RequireEnv("E2E_BASE_URL")) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static async Task<FamilyModel> RegisterAsync(HttpClient client, BoardType board, params int[] classes)
    {
        var body = RegisteredFamilyFixture.ValidBody();
        body.Board = board;
        body.Children =
        [
            .. classes.Select(classLevel => new ChildSaveModel
            {
                Name = "e2e-locked-child-name",
                ClassLevel = classLevel,
                BirthYear = "e2e-locked-birth-year",
                School = "e2e-locked-school",
            }),
        ];

        var response = await client.PostAsJsonAsync(Register, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<FamilyModel>())!;
    }

    // Waits for the Functions container to finish (version.json is written last), then lists the slice.
    private static async Task<string[]> ReadySliceAsync(Guid familyId, Guid childId, int classLevel)
    {
        var container = new BlobServiceClient(AzuriteBlobs).GetBlobContainerClient("family");
        var prefix = $"{familyId:D}/{childId:D}/{classLevel}/content/";
        var ready = container.GetBlobClient(prefix + "version.json");
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline && !await ready.ExistsAsync())
        {
            await Task.Delay(500);
        }

        Assert.True(await ready.ExistsAsync(), "The Functions app did not finish the Content slice in time.");

        var names = new List<string>();
        await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
        {
            names.Add(blob.Name[prefix.Length..]);
        }

        return [.. names.Order(StringComparer.Ordinal)];
    }

    private static string RequireEnv(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is required to run the E2E tests.");
}
