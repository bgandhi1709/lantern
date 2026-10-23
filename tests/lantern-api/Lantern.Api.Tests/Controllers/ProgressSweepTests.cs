using System.Net;
using System.Net.Http.Json;
using Lantern.Api.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Core.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class ProgressSweepTests(AzuriteFixture azurite) : IDisposable
{
    private static readonly DateTimeOffset Day = new(2026, 10, 1, 16, 5, 40, TimeSpan.Zero);

    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    [Fact]
    public async Task DeletingAChild_RemovesItsProgressInEveryClass_AndNothingOfItsSiblingOrAnotherFamily()
    {
        using var client = Client(NewUid());
        using var neighbour = Client(NewUid());
        var family = await RegisterAsync(client, 7, 7);
        var other = await RegisterAsync(neighbour, 7);
        var (leaving, staying) = (family.Children[0].ChildId, family.Children[1].ChildId);
        await WorkedOnAsync(family.FamilyId, leaving, 7);
        await WorkedOnAsync(family.FamilyId, leaving, 8);
        await WorkedOnAsync(family.FamilyId, staying, 7);
        await WorkedOnAsync(other.FamilyId, other.Children[0].ChildId, 7);

        Assert.Equal(HttpStatusCode.Accepted, (await client.DeleteAsync(ApiClientExtensions.Child(leaving))).StatusCode);
        await factory.DeliverAsync();

        Assert.Empty(await ChaptersAsync(family.FamilyId, leaving, 7));
        Assert.Empty(await ChaptersAsync(family.FamilyId, leaving, 8));
        Assert.Empty(await HistoryAsync(family.FamilyId, leaving, 7));
        Assert.Single(await ChaptersAsync(family.FamilyId, staying, 7));
        Assert.Single(await HistoryAsync(family.FamilyId, staying, 7));
        Assert.Single(await ChaptersAsync(other.FamilyId, other.Children[0].ChildId, 7));
    }

    [Fact]
    public async Task DeletingAFamily_RemovesTheProgressOfEveryChild_AndNothingOfAnotherFamily()
    {
        using var client = Client(NewUid());
        using var neighbour = Client(NewUid());
        var family = await RegisterAsync(client, 7, 7);
        var other = await RegisterAsync(neighbour, 7);
        foreach (var child in family.Children)
        {
            await WorkedOnAsync(family.FamilyId, child.ChildId, 7);
        }

        await WorkedOnAsync(other.FamilyId, other.Children[0].ChildId, 7);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(ApiClientExtensions.Family)).StatusCode);
        await factory.DeliverAsync();

        Assert.All(family.Children, child => Assert.Empty(ChaptersAsync(family.FamilyId, child.ChildId, 7).Result));
        Assert.Single(await ChaptersAsync(other.FamilyId, other.Children[0].ChildId, 7));
    }

    [Fact]
    public async Task DeletingAChild_TwiceOver_StillLeavesNothing()
    {
        using var client = Client(NewUid());
        var family = await RegisterAsync(client, 7);
        var child = family.Children[0].ChildId;
        await WorkedOnAsync(family.FamilyId, child, 7);
        await client.DeleteAsync(ApiClientExtensions.Child(child));
        await factory.DeliverAsync();

        await factory.DeliverAsync();

        Assert.Empty(await ChaptersAsync(family.FamilyId, child, 7));
    }

    public void Dispose() => factory.Dispose();

    private async Task WorkedOnAsync(Guid familyId, Guid childId, int classLevel)
    {
        using var scope = factory.Services.CreateScope();
        var progress = scope.ServiceProvider.GetRequiredService<IProgressRepository>();
        var chapter = new ChapterRef(familyId, childId, classLevel, "gegp101");
        await progress.StartAttemptAsync(chapter, Guid.NewGuid(), 2, "v1", Day, CancellationToken.None);
        await progress.RecordCheckAsync(chapter, Guid.NewGuid(), Day, [new CheckedConcept { ConceptId = "c1", Title = "Concept 1", Right = true }], "v1", CancellationToken.None);
    }

    private async Task<IReadOnlyList<ChapterProgress>> ChaptersAsync(Guid familyId, Guid childId, int classLevel)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IProgressRepository>().ChaptersAsync(familyId, childId, classLevel, CancellationToken.None);
    }

    private async Task<IReadOnlyList<Attempt>> HistoryAsync(Guid familyId, Guid childId, int classLevel)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IProgressRepository>().AttemptsAsync(new ChapterRef(familyId, childId, classLevel, "gegp101"), CancellationToken.None);
    }

    private HttpClient Client(string uid) => factory.CreateClient().WithBearer(TestTokens.Create(uid));

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static string Locked(string plaintext) => $"v1.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext))}.{Guid.NewGuid():N}";

    private static async Task<FamilyModel> RegisterAsync(HttpClient client, params int[] classes)
    {
        var body = new FamilyRegisterRequest
        {
            FamilyId = Guid.NewGuid(),
            Region = "Gujarat",
            Board = BoardType.Cbse,
            Language = "gu",
            ParentName = Locked("Meena Patel"),
            ParentEmail = Locked("meena@example.test"),
            RecoveryWrappedKey = Locked("recovery-key"),
            RecoverySalt = Locked("recovery-salt"),
            Consent = new ConsentModel { Accepted = true, NoticeVersion = "2026-09" },
            Children =
            [
                .. classes.Select(classLevel => new ChildSaveModel { Name = Locked("Child"), ClassLevel = classLevel, BirthYear = Locked("2018"), School = Locked("Sunrise School") }),
            ],
        };

        var response = await client.RegisterAsync(body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<FamilyModel>())!;
    }
}
