using System.Net;
using System.Net.Http.Json;
using Lantern.Api.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class ChildDeleteProofTests(AzuriteFixture azurite) : IDisposable
{
    private const string VerifiedEmail = "parent@example.com";

    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    [Fact]
    public async Task Delete_WithNoProof_OrAGarbageOne_Returns403_AndTheChildStays()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0];

        var none = await client.DeleteChildAsync(child.ChildId, proof: null);
        var garbage = await client.DeleteChildAsync(child.ChildId, "garbage.garbage");

        Assert.Equal(HttpStatusCode.Forbidden, none.StatusCode);
        Assert.Equal("proof-required", await none.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, garbage.StatusCode);
        Assert.Equal(2, (await MeAsync(client)).Children.Count);
        Assert.DoesNotContain(factory.Sender.Sent, m => m.Type == ActionType.RemoveWorkspace);
    }

    [Fact]
    public async Task Delete_WithTheChallengeInPlaceOfAProof_Returns403()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0];
        var challenge = await ChallengeAsync(client, child.ChildId);

        var response = await client.DeleteChildAsync(child.ChildId, challenge);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithAProofForAnotherChild_OrForTheFamily_Returns403()
    {
        using var client = await RegisteredAsync();
        var children = (await MeAsync(client)).Children;
        var proofForOther = await client.ProofAsync(ChildScope(children[1].ChildId));
        var proofForFamily = await client.ProofAsync(new { operation = "delete-family" });

        var other = await client.DeleteChildAsync(children[0].ChildId, proofForOther);
        var family = await client.DeleteChildAsync(children[0].ChildId, proofForFamily);

        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, family.StatusCode);
        Assert.Equal(2, (await MeAsync(client)).Children.Count);
    }

    [Fact]
    public async Task Delete_WithAnotherParentsProof_Returns403()
    {
        using var mine = await RegisteredAsync();
        using var theirs = await RegisteredAsync();
        var myChild = (await MeAsync(mine)).Children[0];
        var theirProof = await theirs.ProofAsync(ChildScope(myChild.ChildId));

        var response = await mine.DeleteChildAsync(myChild.ChildId, theirProof);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(2, (await MeAsync(mine)).Children.Count);
    }

    [Fact]
    public async Task Delete_AfterTheProofExpired_Returns403()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0];
        var proof = await client.ProofAsync(ChildScope(child.ChildId));
        factory.Clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        var response = await client.DeleteChildAsync(child.ChildId, proof);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithTheRightProof_Returns202_AndTheChildIsGone()
    {
        using var client = await RegisteredAsync();
        var children = (await MeAsync(client)).Children;

        var response = await client.DeleteChildAsync(children[0].ChildId);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal([children[1].ChildId], (await MeAsync(client)).Children.Select(c => c.ChildId));
    }

    [Fact]
    public async Task Delete_WithAValidProofForAChildOfAnotherFamily_Returns404_AndTouchesNothing()
    {
        using var mine = await RegisteredAsync();
        using var theirs = await RegisteredAsync();
        var theirChild = (await MeAsync(theirs)).Children[0];
        var proof = await mine.ProofAsync(new { operation = "delete-child", target = theirChild.ChildId });

        var response = await mine.DeleteChildAsync(theirChild.ChildId, proof);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("child-not-found", await response.ProblemCodeAsync());
        Assert.Equal(2, (await MeAsync(theirs)).Children.Count);
        Assert.DoesNotContain(factory.Sender.Sent, m => m.Type == ActionType.RemoveWorkspace);
    }

    private static async Task<string> ChallengeAsync(HttpClient client, Guid childId)
    {
        var response = await client.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope(childId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<CodeChallengeModel>())!.Challenge;
    }

    private async Task<HttpClient> RegisteredAsync(string email = VerifiedEmail, bool? emailVerified = true)
    {
        var client = factory.CreateClient().WithBearer(TestTokens.Create($"uid-{Guid.NewGuid():N}", email: email, emailVerified: emailVerified));
        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(RegisterBody())).StatusCode);
        await factory.DeliverAsync();

        return client;
    }

    private static async Task<FamilyModel> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me))!;

    private static object ChildScope(Guid childId) => new { operation = "delete-child", target = childId };

    private static FamilyRegisterRequest RegisterBody() =>
        new()
        {
            FamilyId = Guid.NewGuid(),
            Region = "Gujarat",
            Board = BoardType.Cbse,
            Language = "gu",
            ParentName = "locked-parent-name",
            ParentEmail = "locked-parent-email",
            RecoveryWrappedKey = "locked-recovery-key",
            RecoverySalt = "salt-2",
            Consent = new ConsentModel { Accepted = true, NoticeVersion = "2026-09" },
            Children =
            [
                new ChildSaveModel { Name = "Aarav", ClassLevel = 5, BirthYear = $"locked-{DateTime.UtcNow.Year - 10}", School = "locked-school" },
                new ChildSaveModel { Name = "Diya", ClassLevel = 3, BirthYear = $"locked-{DateTime.UtcNow.Year - 8}", School = "locked-school" },
            ],
        };

    public void Dispose() => factory.Dispose();
}
