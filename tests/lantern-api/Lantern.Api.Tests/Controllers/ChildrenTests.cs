extern alias Functions;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lantern.Api.Models;
using Lantern.Api.Services;
using Functions::Lantern.Functions.Handler;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Core.Repository;
using Lantern.Repository;
using Lantern.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class ChildrenTests(AzuriteFixture azurite) : IDisposable
{
    private static readonly RowKeyService KeyService = new();

    private static readonly int Year = DateTime.UtcNow.Year;

    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    private BlobContainerClient Blobs => azurite.CreateBlobClient().GetBlobContainerClient("family");

    private TableClient Families => azurite.CreateClient().GetTableClient("families");

    private TableClient Actions => azurite.CreateClient().GetTableClient("actions");

    private PendingActionResender Resender => factory.Services.GetRequiredService<PendingActionResender>();

    private IReadOnlyList<ActionMessage> Removals => [.. factory.Sender.Sent.Where(m => m.Type == ActionType.RemoveWorkspace)];

    // ---- add ----

    [Fact]
    public async Task Add_Valid_Returns201_ListsItAfterTheOthersAndStartsItsWorkspace()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var id = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, "Kavya", 4, "Green School"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(ApiClientExtensions.Child(id), response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<ChildModel>();
        Assert.Equal((id, "Kavya", "Green School", 4), (created!.ChildId, created.Name, created.School, created.ClassLevel));
        var after = await MeAsync(client);
        Assert.Equal(["Aarav", "Diya", "Kavya"], after.Children.Select(c => c.Name));
        Assert.False(await Blobs.GetBlobClient($"{me.FamilyId:D}/{id:D}/4/class.json").ExistsAsync());

        await factory.DeliverAsync();

        Assert.True(await Blobs.GetBlobClient($"{me.FamilyId:D}/{id:D}/4/class.json").ExistsAsync());
    }

    [Fact]
    public async Task Add_SameChildIdTwice_Returns200AndAddsOne()
    {
        using var client = await RegisteredAsync();
        var id = Guid.NewGuid();
        await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, "Kavya"));

        var again = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, "Kavya"));

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(3, (await MeAsync(client)).Children.Count);
    }

    [Fact]
    public async Task Add_TwinsWithTheSameName_AreTwoChildren()
    {
        using var client = await RegisteredAsync();

        await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Aarav"));

        Assert.Equal(["Aarav", "Diya", "Aarav"], (await MeAsync(client)).Children.Select(c => c.Name));
    }

    [Fact]
    public async Task Add_SeventhChild_Returns409AndStartsNoWorkspace()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        foreach (var n in Enumerable.Range(0, 4))
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), $"C{n}"))).StatusCode);
        }
        var seventh = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(seventh, "Extra"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("child-limit-reached", await response.ProblemCodeAsync());
        Assert.Equal(6, (await MeAsync(client)).Children.Count);
        await factory.DeliverAsync();
        Assert.False(await Blobs.GetBlobClient($"{me.FamilyId:D}/{seventh:D}/1/class.json").ExistsAsync());
    }

    [Fact]
    public async Task Add_TenAtOnceAtFive_NeverPassesSix()
    {
        using var client = await RegisteredAsync();
        for (var n = 0; n < 3; n++)
        {
            await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), $"C{n}"));
        }

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(n => client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), $"R{n}")))
        );

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(6, (await MeAsync(client)).Children.Count);
    }

    [Theory]
    [InlineData("class-zero")]
    [InlineData("class-eleven")]
    [InlineData("empty-name")]
    [InlineData("overlong-name")]
    [InlineData("empty-birth-year")]
    [InlineData("overlong-school")]
    [InlineData("empty-child-id")]
    public async Task Add_InvalidBody_Returns400AndStoresNothing(string scenario)
    {
        using var client = await RegisteredAsync();
        var body = NewChild(Guid.NewGuid(), "Kavya");
        switch (scenario)
        {
            case "class-zero": body.ClassLevel = 0; break;
            case "class-eleven": body.ClassLevel = 11; break;
            case "empty-name": body.Name = ""; break;
            case "overlong-name": body.Name = new string('x', 401); break;
            case "empty-birth-year": body.BirthYear = ""; break;
            case "overlong-school": body.School = new string('x', 401); break;
            case "empty-child-id": body.ChildId = Guid.Empty; break;
        }

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, (await MeAsync(client)).Children.Count);
    }

    [Fact]
    public async Task Add_WithoutToken_Returns401_AndUnregistered_Returns404()
    {
        using var anonymous = factory.CreateClient();
        using var unregistered = factory.CreateClient().WithBearer(TestTokens.Create(NewUid()));

        var denied = await anonymous.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Kavya"));
        var missing = await unregistered.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Kavya"));

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not-registered", await missing.ProblemCodeAsync());
    }

    [Fact]
    public async Task Add_WhenTheWorkspaceFails_KeepsTheChild_AndRedeliveryStartsIt()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var id = Guid.NewGuid();
        var workspaces = new Mock<IWorkspaceStore>();
        workspaces
            .Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), 9, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, "blob down"));
        using var failing = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(workspaces.Object)));

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, "Kavya", 9))).StatusCode);
        await Assert.ThrowsAsync<RequestFailedException>(() =>
            failing.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(factory.Sender.Sent[^1], CancellationToken.None)
        );
        await factory.DeliverAsync();

        Assert.Equal(3, (await MeAsync(client)).Children.Count);
        Assert.True(await Blobs.GetBlobClient($"{me.FamilyId:D}/{id:D}/9/class.json").ExistsAsync());
    }

    [Fact]
    public async Task Add_ThenDeleteBeforeTheWorkspaceStarts_LeavesNoBlobs()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var id = Guid.NewGuid();
        await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, "Kavya", 4));
        await client.DeleteAsync(ApiClientExtensions.Child(id));

        await factory.DeliverAsync();

        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/{id:D}/"));
        Assert.Empty(await LedgerAsync(me.FamilyId));
    }

    [Fact]
    public async Task Add_StoredRow_HoldsExactlyTheLockedValuesTheClientSent()
    {
        using var client = await RegisteredAsync();
        var body = NewChild(Guid.NewGuid(), $"locked-name-{Guid.NewGuid():N}", 3, $"locked-school-{Guid.NewGuid():N}");

        await client.PostAsJsonAsync(ApiClientExtensions.Children, body);

        var row = await Families.GetEntityAsync<TableEntity>(
            KeyService.FamilyPartition((await MeAsync(client)).FamilyId),
            KeyService.ChildRowKey(body.ChildId)
        );
        Assert.Equal(body.Name, row.Value.GetString("Name"));
        Assert.Equal(body.School, row.Value.GetString("School"));
        Assert.Equal(body.BirthYear, row.Value.GetString("BirthYear"));
    }

    [Fact]
    public async Task Add_AnSscFamilysChild_AboveClassFive_Returns400ClassNotAvailable_AndStartsNoWorkspace()
    {
        using var client = await RegisteredAsync(BoardType.Ssc);
        var id = Guid.NewGuid();

        var six = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, "Kavya", 6));
        var five = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Kavya", 5));

        Assert.Equal(HttpStatusCode.BadRequest, six.StatusCode);
        Assert.Equal("class-not-available", await six.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Created, five.StatusCode);
        Assert.Equal(3, (await MeAsync(client)).Children.Count);
        Assert.DoesNotContain(factory.Sender.Sent, m => m.Payload.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Add_ChildIdOfAChildBeingDeleted_Returns409()
    {
        using var client = await RegisteredAsync();
        var first = (await MeAsync(client)).Children[0];
        await client.DeleteAsync(ApiClientExtensions.Child(first.ChildId));

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(first.ChildId, "Again"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("child-deleting", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Add_AfterADelete_FreesTheSlotAtOnce()
    {
        using var client = await RegisteredAsync();
        foreach (var n in Enumerable.Range(0, 4))
        {
            await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), $"C{n}"));
        }
        await client.DeleteAsync(ApiClientExtensions.Child((await MeAsync(client)).Children[0].ChildId));

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Fresh"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Fresh", (await MeAsync(client)).Children[^1].Name);
    }

    // ---- edit ----

    [Fact]
    public async Task Edit_ChangesNameSchoolAndBirthYear_NotTheClass()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0];

        var response = await client.PutAsJsonAsync(
            ApiClientExtensions.Child(child.ChildId),
            new { Name = "Aarav K", School = "New School", BirthYear = "locked-7", ClassLevel = 9 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var now = (await MeAsync(client)).Children[0];
        Assert.Equal(("Aarav K", "New School", "locked-7", child.ClassLevel), (now.Name, now.School, now.BirthYear, now.ClassLevel));
    }

    [Fact]
    public async Task Edit_WithoutASchool_Returns400_AndStoresTheLockedValuesAsSentWhenGiven()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var child = me.Children[0];
        var name = $"renamed-{Guid.NewGuid():N}";

        var missing = await client.PutAsJsonAsync(ApiClientExtensions.Child(child.ChildId), new { Name = name, BirthYear = child.BirthYear });
        var given = await client.PutAsJsonAsync(ApiClientExtensions.Child(child.ChildId), new { Name = name, School = "locked-empty-school", BirthYear = child.BirthYear });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, given.StatusCode);
        var row = await Families.GetEntityAsync<TableEntity>(KeyService.FamilyPartition(me.FamilyId), KeyService.ChildRowKey(child.ChildId));
        Assert.Equal(name, row.Value.GetString("Name"));
        Assert.Equal("locked-empty-school", row.Value.GetString("School"));
    }

    [Fact]
    public async Task Edit_AnotherFamilysChild_Returns404AndChangesNothing()
    {
        using var mine = await RegisteredAsync();
        using var theirs = await RegisteredAsync();
        var theirChild = (await MeAsync(theirs)).Children[0];

        var response = await mine.PutAsJsonAsync(
            ApiClientExtensions.Child(theirChild.ChildId),
            new { Name = "Hijacked", School = "s", BirthYear = theirChild.BirthYear }
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("child-not-found", await response.ProblemCodeAsync());
        Assert.Equal("Aarav", (await MeAsync(theirs)).Children[0].Name);
    }

    [Fact]
    public async Task Edit_UnknownDeletingOrInvalid_IsRefused()
    {
        using var client = await RegisteredAsync();
        var children = (await MeAsync(client)).Children;
        await client.DeleteAsync(ApiClientExtensions.Child(children[1].ChildId));

        var unknown = await client.PutAsJsonAsync(ApiClientExtensions.Child(Guid.NewGuid()), new { Name = "X", School = "s", BirthYear = "y" });
        var deleting = await client.PutAsJsonAsync(ApiClientExtensions.Child(children[1].ChildId), new { Name = "X", School = "s", BirthYear = "y" });
        var badYear = await client.PutAsJsonAsync(ApiClientExtensions.Child(children[0].ChildId), new { Name = "X", School = "s", BirthYear = "" });
        var badName = await client.PutAsJsonAsync(ApiClientExtensions.Child(children[0].ChildId), new { Name = "", School = "s", BirthYear = "y" });

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleting.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badYear.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badName.StatusCode);
        Assert.Equal("Aarav", (await MeAsync(client)).Children[0].Name);
    }

    [Fact]
    public async Task Edit_WithoutToken_Returns401_AndUnregistered_Returns404()
    {
        using var anonymous = factory.CreateClient();
        using var unregistered = factory.CreateClient().WithBearer(TestTokens.Create(NewUid()));
        var body = new { Name = "X", School = "s", BirthYear = "y" };

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(ApiClientExtensions.Child(Guid.NewGuid()), body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unregistered.PutAsJsonAsync(ApiClientExtensions.Child(Guid.NewGuid()), body)).StatusCode);
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_Returns202_HidesTheChildAtOnce_AndTheHandlerRemovesEverythingOfItAndNothingElse()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var (gone, kept) = (me.Children[0], me.Children[1]);
        // The Child moved from Class 5 to Class 6 (issue #50 will do this): two Workspaces.
        await new WorkspaceStore(Blobs, createContainer: false).CreateAsync(me.FamilyId, gone.ChildId, 6, CancellationToken.None);

        var response = await client.DeleteAsync(ApiClientExtensions.Child(gone.ChildId));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal([kept.ChildId], (await MeAsync(client)).Children.Select(c => c.ChildId));
        var message = Assert.Single(Removals);
        Assert.Equal(ActionType.RemoveWorkspace, message.Type);
        Assert.Single(await LedgerAsync(me.FamilyId));
        Assert.True(await Blobs.GetBlobClient($"{me.FamilyId:D}/{gone.ChildId:D}/{gone.ClassLevel}/class.json").ExistsAsync());

        await factory.DeliverAsync();

        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/{gone.ChildId:D}/"));
        Assert.True(await Blobs.GetBlobClient($"{me.FamilyId:D}/{kept.ChildId:D}/{kept.ClassLevel}/class.json").ExistsAsync());
        Assert.Empty(await LedgerAsync(me.FamilyId));
        Assert.Empty(await Families.QueryAsync<TableEntity>(r => r.PartitionKey == KeyService.FamilyPartition(me.FamilyId) && r.RowKey == KeyService.ChildRowKey(gone.ChildId)).ToListAsync());
    }

    [Fact]
    public async Task Delete_Repeated_Returns202_SendsAgain_AndTheSecondDeliveryDoesNothing()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0];

        var first = await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));
        var second = await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));
        await factory.DeliverAsync();
        var third = await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, third.StatusCode);
        Assert.Equal(2, Removals.Count);
    }

    [Fact]
    public async Task Delete_AnotherFamilysChild_Returns404_SendsNothingAndTouchesNothingOfIt()
    {
        using var mine = await RegisteredAsync();
        using var theirs = await RegisteredAsync();
        var theirFamily = await MeAsync(theirs);
        var theirChild = theirFamily.Children[0];

        var response = await mine.DeleteAsync(ApiClientExtensions.Child(theirChild.ChildId));
        await factory.DeliverAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("child-not-found", await response.ProblemCodeAsync());
        Assert.Empty(Removals);
        Assert.Equal(2, (await MeAsync(theirs)).Children.Count);
        Assert.NotEmpty(await BlobNamesAsync($"{theirFamily.FamilyId:D}/{theirChild.ChildId:D}/"));
        Assert.Empty(await LedgerAsync(theirFamily.FamilyId));
        var row = await Families.GetEntityAsync<TableEntity>(KeyService.FamilyPartition(theirFamily.FamilyId), KeyService.ChildRowKey(theirChild.ChildId));
        Assert.Equal("Active", row.Value.GetString("Status"));
    }

    [Fact]
    public async Task Delete_UnknownChild_UnregisteredCaller_AndNoToken_AreRefused()
    {
        using var client = await RegisteredAsync();
        using var unregistered = factory.CreateClient().WithBearer(TestTokens.Create(NewUid()));
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(ApiClientExtensions.Child(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unregistered.DeleteAsync(ApiClientExtensions.Child(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(ApiClientExtensions.Child(Guid.NewGuid()))).StatusCode);
        Assert.Equal(2, (await MeAsync(client)).Children.Count);
        Assert.Empty(Removals);
    }

    [Fact]
    public async Task Delete_EveryChild_LeavesAnEmptyFamily_ThatCanAddAgain()
    {
        using var client = await RegisteredAsync();
        foreach (var child in (await MeAsync(client)).Children)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId))).StatusCode);
        }
        await factory.DeliverAsync();

        Assert.Empty((await MeAsync(client)).Children);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(Guid.NewGuid(), "Fresh"))).StatusCode);
    }

    [Fact]
    public async Task Delete_WhenTheQueueIsDown_StillReturns202_AndTheStartupPassSendsItAgain()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var child = me.Children[0];
        factory.Sender.Down = true;

        var response = await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(Removals);
        Assert.Single(await LedgerAsync(me.FamilyId));
        Assert.Single((await MeAsync(client)).Children);

        factory.Sender.Down = false;
        await AgeLedgerAsync(me.FamilyId);
        await Resender.RunOnceAsync(CancellationToken.None);
        await factory.DeliverAsync();

        Assert.Empty(await LedgerAsync(me.FamilyId));
        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/{child.ChildId:D}/"));
    }

    [Fact]
    public async Task StartupPass_LeavesAnActionSentMomentsAgoAlone()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        await client.DeleteAsync(ApiClientExtensions.Child(me.Children[0].ChildId));

        await Resender.RunOnceAsync(CancellationToken.None);

        Assert.Single(Removals);
    }

    [Fact]
    public async Task Handler_WhenTheBlobStepFails_KeepsTheRowAndTheLedger_AndFinishesWhenTheMessageIsDeliveredAgain()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var child = me.Children[0];
        await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));
        var failing = new Mock<IWorkspaceStore>();
        failing
            .Setup(s => s.RemoveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "blob down"));
        using var broken = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(failing.Object)));

        await Assert.ThrowsAsync<RequestFailedException>(() =>
            broken.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(Removals[0], CancellationToken.None)
        );

        Assert.Single(await LedgerAsync(me.FamilyId));
        Assert.True(await Families.GetEntityIfExistsAsync<TableEntity>(KeyService.FamilyPartition(me.FamilyId), KeyService.ChildRowKey(child.ChildId)) is { HasValue: true });
        Assert.NotEmpty(await BlobNamesAsync($"{me.FamilyId:D}/{child.ChildId:D}/"));
        Assert.Single((await MeAsync(client)).Children);

        await factory.DeliverAsync();

        Assert.Empty(await LedgerAsync(me.FamilyId));
        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/{child.ChildId:D}/"));
    }

    [Fact]
    public async Task Handler_ResumesAfterACrashBetweenTheRowDeleteAndTheLastStep()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        var child = me.Children[0];
        await client.DeleteAsync(ApiClientExtensions.Child(child.ChildId));
        await Blobs.DeleteBlobIfExistsAsync($"{me.FamilyId:D}/{child.ChildId:D}/{child.ClassLevel}/class.json");
        await Families.DeleteEntityAsync(KeyService.FamilyPartition(me.FamilyId), KeyService.ChildRowKey(child.ChildId));

        await factory.DeliverAsync();

        Assert.Empty(await LedgerAsync(me.FamilyId));
    }

    [Fact]
    public async Task Handler_TheSameMessageTwiceAtOnce_FinishesWithoutError()
    {
        using var client = await RegisteredAsync();
        var me = await MeAsync(client);
        await client.DeleteAsync(ApiClientExtensions.Child(me.Children[0].ChildId));
        var dispatcher = factory.Services.GetRequiredService<IActionDispatcher>();
        var message = Removals[0];

        await Task.WhenAll(
            dispatcher.DispatchAsync(message, CancellationToken.None),
            dispatcher.DispatchAsync(message, CancellationToken.None)
        );

        Assert.Empty(await LedgerAsync(me.FamilyId));
        Assert.Empty(await BlobNamesAsync($"{me.FamilyId:D}/{me.Children[0].ChildId:D}/"));
    }

    [Fact]
    public async Task Handler_AMessageWithNoLedgerRow_TouchesNothing_AndTheAnswerLibraryIsNeverReached()
    {
        using var client = await RegisteredAsync();
        using var other = await RegisteredAsync();
        var me = await MeAsync(client);
        var otherFamily = await MeAsync(other);
        await Blobs.GetBlobClient("answers/stage-3/q1.json").UploadAsync(BinaryData.FromString("{}"), overwrite: true);
        await Families.UpsertEntityAsync(new TableEntity("answer-library", "q1") { ["Text"] = "scrubbed" });
        // A delivery that outlives its action (or one that was never recorded) names the other Family's Child.
        var stray = new ActionMessage(
            "stray",
            ActionType.RemoveWorkspace,
            JsonSerializer.Serialize(new RemoveWorkspacePayload(otherFamily.FamilyId, otherFamily.Children[0].ChildId), JsonSerializerOptions.Web)
        );
        await client.DeleteAsync(ApiClientExtensions.Child(me.Children[0].ChildId));

        await factory.Services.GetRequiredService<IActionDispatcher>().DispatchAsync(stray, CancellationToken.None);
        await factory.DeliverAsync();

        Assert.NotEmpty(await BlobNamesAsync($"{otherFamily.FamilyId:D}/{otherFamily.Children[0].ChildId:D}/"));
        Assert.True(await Blobs.GetBlobClient("answers/stage-3/q1.json").ExistsAsync());
        Assert.True((await Families.GetEntityIfExistsAsync<TableEntity>("answer-library", "q1")).HasValue);
        Assert.Equal(2, (await MeAsync(other)).Children.Count);
    }

    // ---- cross-cutting ----

    [Fact]
    public async Task Add_OversizedBody_IsRefusedBeforeItIsRead()
    {
        using var client = await RegisteredAsync();
        var body = NewChild(Guid.NewGuid(), "Kavya", 1, new string('x', 5000));

        var response = await client.PostAsJsonAsync(ApiClientExtensions.Children, body);

        Assert.True(response.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest);
        Assert.Equal(2, (await MeAsync(client)).Children.Count);
    }


    [Fact]
    public async Task Children_PastThePerMinuteLimit_Get429_ForThatCallerOnly()
    {
        using var noisy = await RegisteredAsync();
        using var quiet = await RegisteredAsync();

        HttpStatusCode last = default;
        for (var n = 0; n < 40; n++)
        {
            last = (await noisy.DeleteAsync(ApiClientExtensions.Child(Guid.NewGuid()))).StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
        Assert.Equal(HttpStatusCode.NotFound, (await quiet.DeleteAsync(ApiClientExtensions.Child(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task Requests_NeverLogLockedChildValues()
    {
        using var client = await RegisteredAsync();
        var name = $"Child-{Guid.NewGuid():N}";
        var school = $"School-{Guid.NewGuid():N}";
        var id = Guid.NewGuid();

        await client.PostAsJsonAsync(ApiClientExtensions.Children, NewChild(id, name, 2, school));
        await client.PutAsJsonAsync(ApiClientExtensions.Child(id), new { Name = name + "x", School = school + "x", BirthYear = "y" });
        await client.DeleteAsync(ApiClientExtensions.Child(id));
        await factory.DeliverAsync();

        Assert.NotEmpty(factory.Logs.Entries);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Contains(name, StringComparison.Ordinal) || e.Contains(school, StringComparison.Ordinal));
    }

    // ---- helpers ----

    private async Task<HttpClient> RegisteredAsync(BoardType board = BoardType.Cbse)
    {
        var client = factory.CreateClient().WithBearer(TestTokens.Create(NewUid()));
        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(RegisterBody(board))).StatusCode);
        await factory.DeliverAsync();

        return client;
    }

    private static async Task<FamilyModel> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me))!;

    private async Task<List<string>> BlobNamesAsync(string prefix) =>
    await Blobs
        .GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None)
        .Select(blob => blob.Name)
        .ToListAsync();

    private async Task<List<TableEntity>> LedgerAsync(Guid familyId) =>
        await Actions
            .QueryAsync<TableEntity>(r => r.PartitionKey == nameof(ActionType.RemoveWorkspace) && r.RowKey.CompareTo($"{familyId:N}_") >= 0 && r.RowKey.CompareTo($"{familyId:N}`") < 0)
            .ToListAsync();

    private async Task AgeLedgerAsync(Guid familyId)
    {
        foreach (var row in await LedgerAsync(familyId))
        {
            row["LastSentAt"] = DateTimeOffset.UtcNow.AddMinutes(-10);
            await Actions.UpdateEntityAsync(row, row.ETag, TableUpdateMode.Merge);
        }
    }

    private static ChildAddModel NewChild(Guid id, string name, int classLevel = 1, string school = "locked-school") =>
        new() { ChildId = id, Name = name, ClassLevel = classLevel, BirthYear = $"locked-{Year - 8}", School = school };

    // The server never reads locked values, so the tests send short opaque strings.
    private static FamilyRegisterRequest RegisterBody(BoardType board) =>
        new()
        {
            FamilyId = Guid.NewGuid(),
            Region = "Gujarat",
            Board = board,
            Language = "gu",
            ParentName = "locked-parent-name",
            ParentEmail = "locked-parent-email",
            PassphraseWrappedKey = "locked-passphrase-key",
            PassphraseSalt = "salt-1",
            RecoveryWrappedKey = "locked-recovery-key",
            RecoverySalt = "salt-2",
            Consent = new ConsentModel { Accepted = true, NoticeVersion = "2026-09" },
            Children =
            [
                new ChildSaveModel { Name = "Aarav", ClassLevel = 5, BirthYear = $"locked-{Year - 10}", School = "locked-school" },
                new ChildSaveModel { Name = "Diya", ClassLevel = 3, BirthYear = $"locked-{Year - 8}", School = "locked-school" },
            ],
        };

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    public void Dispose() => factory.Dispose();
}
