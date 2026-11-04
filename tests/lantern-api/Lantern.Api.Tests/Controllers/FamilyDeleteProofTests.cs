using System.Net;
using System.Net.Http.Json;
using Lantern.Api.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class FamilyDeleteProofTests(AzuriteFixture azurite) : IDisposable
{
    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    [Fact]
    public async Task Delete_WithNoProof_OrAGarbageOne_Returns403_AndTheFamilyStays()
    {
        using var client = await RegisteredAsync();

        var none = await client.DeleteFamilyAsync(proof: null);
        var garbage = await client.DeleteFamilyAsync("garbage.garbage");

        Assert.Equal(HttpStatusCode.Forbidden, none.StatusCode);
        Assert.Equal("proof-required", await none.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, garbage.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ApiClientExtensions.Me)).StatusCode);
        Assert.DoesNotContain(factory.Sender.Sent, m => m.Type == ActionType.RemoveFamily);
    }

    [Fact]
    public async Task Delete_WithAProofForAChild_OrForAnotherOperation_Returns403()
    {
        using var client = await RegisteredAsync();
        var child = (await client.GetFromJsonAsync<FamilyModel>(ApiClientExtensions.Me))!.Children[0];
        var forChild = await client.ProofAsync(new { operation = "delete-child", target = child.ChildId });
        var forOther = await client.ProofAsync(new { operation = "add-parent" });

        var asChild = await client.DeleteFamilyAsync(forChild);
        var asOther = await client.DeleteFamilyAsync(forOther);

        Assert.Equal(HttpStatusCode.Forbidden, asChild.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asOther.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ApiClientExtensions.Me)).StatusCode);
    }

    [Fact]
    public async Task Delete_WithAnotherParentsProof_OrAnExpiredOne_Returns403()
    {
        using var mine = await RegisteredAsync();
        using var theirs = await RegisteredAsync();
        var theirProof = await theirs.ProofAsync(ApiClientExtensions.FamilyScope);
        var expired = await mine.ProofAsync(ApiClientExtensions.FamilyScope);
        factory.Clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        var theirs403 = await mine.DeleteFamilyAsync(theirProof);
        var expired403 = await mine.DeleteFamilyAsync(expired);

        Assert.Equal(HttpStatusCode.Forbidden, theirs403.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, expired403.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mine.GetAsync(ApiClientExtensions.Me)).StatusCode);
    }

    [Fact]
    public async Task Delete_WithTheFamilyProof_Returns204_AndARepeatFindsNoFamily()
    {
        using var client = await RegisteredAsync();
        var proof = await client.ProofAsync(ApiClientExtensions.FamilyScope);

        var first = await client.DeleteFamilyAsync(proof);
        var repeat = await client.DeleteFamilyAsync(proof);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal("not-registered", await (await client.GetAsync(ApiClientExtensions.Me)).ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, repeat.StatusCode);
        Assert.Equal("not-registered", await repeat.ProblemCodeAsync());
    }

    private async Task<HttpClient> RegisteredAsync()
    {
        var client = factory
            .CreateClient()
            .WithBearer(TestTokens.Create($"uid-{Guid.NewGuid():N}", email: "parent@example.com", emailVerified: true));
        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(RegisterBody())).StatusCode);

        return client;
    }

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
