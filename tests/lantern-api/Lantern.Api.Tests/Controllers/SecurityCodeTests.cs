using System.Net;
using System.Net.Http.Json;
using Lantern.Api.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class SecurityCodeTests(AzuriteFixture azurite) : IDisposable
{
    private const string VerifiedEmail = "parent@example.com";

    private readonly LanternApiFactory factory = new(azurite.ConnectionString);

    [Fact]
    public async Task Request_ForOneOfMyChildren_EmailsTheCodeToTheTokensVerifiedAddress_AndReturnsAChallengeWithoutTheCode()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0];

        var response = await client.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope(child.ChildId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var email = Assert.Single(factory.Emails.Sent);
        Assert.Equal(VerifiedEmail, email.To);
        Assert.Contains(FixedCodeGenerator.Code, email.Text, StringComparison.Ordinal);
        var challenge = (await response.Content.ReadFromJsonAsync<CodeChallengeModel>())!.Challenge;
        Assert.False(string.IsNullOrWhiteSpace(challenge));
        Assert.DoesNotContain(FixedCodeGenerator.Code, challenge, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Request_WithAnEmailThatIsNotVerified_Returns403_AndSendsNothing(bool? emailVerified)
    {
        using var client = await RegisteredAsync(emailVerified: emailVerified);
        var child = (await MeAsync(client)).Children[0];

        var response = await client.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope(child.ChildId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("email-not-verified", await response.ProblemCodeAsync());
        Assert.Empty(factory.Emails.Sent);
    }

    [Fact]
    public async Task Request_WhenTheEmailCannotBeSent_Returns502_WithNoChallenge()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0];
        factory.Emails.Down = true;

        var response = await client.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope(child.ChildId));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("email-not-sent", await response.ProblemCodeAsync());
        Assert.DoesNotContain("challenge", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Request_NeverLogsTheCode()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0];

        await client.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope(child.ChildId));

        Assert.NotEmpty(factory.Logs.Entries);
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Contains(FixedCodeGenerator.Code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Verify_WithTheRightCode_ReturnsAProofThatIsNeitherTheChallengeNorTheCode()
    {
        using var client = await RegisteredAsync();
        var challenge = await ChallengeAsync(client, (await MeAsync(client)).Children[0].ChildId);

        var response = await client.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge, code = FixedCodeGenerator.Code });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proof = (await response.Content.ReadFromJsonAsync<CodeProofModel>())!.Proof;
        Assert.False(string.IsNullOrWhiteSpace(proof));
        Assert.NotEqual(challenge, proof);
        Assert.DoesNotContain(FixedCodeGenerator.Code, proof, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_WithAWrongCode_Returns400_AndNoProof()
    {
        using var client = await RegisteredAsync();
        var challenge = await ChallengeAsync(client, (await MeAsync(client)).Children[0].ChildId);

        var response = await client.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge, code = "654321" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("code-wrong", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-challenge")]
    [InlineData("a.b.c")]
    [InlineData("....")]
    public async Task Verify_WithAChallengeThatIsNotOurs_Returns400(string challenge)
    {
        using var client = await RegisteredAsync();

        var response = await client.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge, code = FixedCodeGenerator.Code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Verify_WithAChallengeWhoseScopeWasChanged_Returns400()
    {
        using var client = await RegisteredAsync();
        var children = (await MeAsync(client)).Children;
        var challenge = await ChallengeAsync(client, children[0].ChildId);
        var other = await ChallengeAsync(client, children[1].ChildId);
        var forged = $"{other.Split('.')[0]}.{challenge.Split('.')[1]}";

        var response = await client.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge = forged, code = FixedCodeGenerator.Code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("code-wrong", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Verify_AfterFiveMinutes_Returns410_EvenWithTheRightCode()
    {
        using var client = await RegisteredAsync();
        var challenge = await ChallengeAsync(client, (await MeAsync(client)).Children[0].ChildId);
        factory.Clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        var response = await client.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge, code = FixedCodeGenerator.Code });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("code-expired", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Verify_AnotherParentsChallenge_WithTheRightCode_Returns400()
    {
        using var mine = await RegisteredAsync();
        using var theirs = await RegisteredAsync();
        var challenge = await ChallengeAsync(theirs, (await MeAsync(theirs)).Children[0].ChildId);

        var response = await mine.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge, code = FixedCodeGenerator.Code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("code-wrong", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData("")]
    public async Task Verify_WithACodeThatIsNotSixDigits_Returns400(string code)
    {
        using var client = await RegisteredAsync();
        var challenge = await ChallengeAsync(client, (await MeAsync(client)).Children[0].ChildId);

        var response = await client.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge, code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Delete-Child")]
    [InlineData("delete_child")]
    [InlineData("delete child")]
    [InlineData("-delete")]
    public async Task Request_WithAnOperationThatIsNotAKebabName_Returns400_AndSendsNothing(string operation)
    {
        using var client = await RegisteredAsync();

        var response = await client.PostAsJsonAsync(ApiClientExtensions.SecurityCode, new { operation });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Emails.Sent);
    }

    [Fact]
    public async Task Request_ForAnyOperation_EmailsTheCode_WithoutNamingTheOperationOrTheTarget()
    {
        using var client = await RegisteredAsync();

        var response = await client.PostAsJsonAsync(ApiClientExtensions.SecurityCode, new { operation = "add-parent", target = "a-target-id" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var email = Assert.Single(factory.Emails.Sent);
        Assert.Contains(FixedCodeGenerator.Code, email.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("add-parent", email.Text + email.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("a-target-id", email.Text + email.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestAndVerify_LeaveATraceByOperation_ButNeverTheCode()
    {
        using var client = await RegisteredAsync();
        var child = (await MeAsync(client)).Children[0].ChildId;
        var challenge = await ChallengeAsync(client, child);

        await client.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge, code = FixedCodeGenerator.Code });

        Assert.Contains(factory.Logs.Entries, entry => entry.Contains("one-time code was sent for delete-child", StringComparison.Ordinal));
        Assert.Contains(factory.Logs.Entries, entry => entry.Contains("one-time code was proven for delete-child", StringComparison.Ordinal));
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Contains(FixedCodeGenerator.Code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Verify_AfterFiveMinutes_LogsTheRefusalByReason()
    {
        using var client = await RegisteredAsync();
        var challenge = await ChallengeAsync(client, (await MeAsync(client)).Children[0].ChildId);
        factory.Clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        await client.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge, code = FixedCodeGenerator.Code });

        Assert.Contains(factory.Logs.Entries, entry => entry.Contains("refused: CodeExpired", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Request_WithAnEmailThatIsNotVerified_LogsTheRefusalByReason_NotTheAddress()
    {
        using var client = await RegisteredAsync(emailVerified: false);
        var child = (await MeAsync(client)).Children[0].ChildId;

        await client.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope(child));

        Assert.Contains(factory.Logs.Entries, entry => entry.Contains("refused: EmailNotVerified", StringComparison.Ordinal));
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Contains(VerifiedEmail, StringComparison.Ordinal));
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
