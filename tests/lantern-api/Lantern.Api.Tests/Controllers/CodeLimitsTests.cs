using System.Net;
using System.Net.Http.Json;
using Lantern.Api.Models;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Constants;

namespace Lantern.Api.Tests.Controllers;

[Collection(ApiCollection.Name)]
public sealed class CodeLimitsTests(AzuriteFixture azurite) : IDisposable
{
    private readonly List<LanternApiFactory> factories = [];

    [Fact]
    public async Task Request_TwiceWithinAMinute_Returns429_AndSendsOneEmail_ForThatParentOnly()
    {
        var factory = Factory(requestsPerMinute: 1);
        using var busy = await RegisteredAsync(factory);
        using var quiet = await RegisteredAsync(factory);
        var child = (await MeAsync(busy)).Children[0].ChildId;

        var first = await busy.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope(child));
        var second = await busy.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope(child));
        var other = await quiet.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope((await MeAsync(quiet)).Children[0].ChildId));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("code-too-soon", await second.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.Equal(2, factory.Emails.Sent.Count);
    }

    [Fact]
    public async Task Request_ASixthTimeInTheHour_Returns429()
    {
        var factory = Factory(requestsPerHour: 5);
        using var client = await RegisteredAsync(factory);
        var child = (await MeAsync(client)).Children[0].ChildId;

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            statuses.Add((await client.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope(child))).StatusCode);
        }

        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.OK, 5), HttpStatusCode.TooManyRequests], statuses);
        Assert.Equal(5, factory.Emails.Sent.Count);
    }

    [Fact]
    public async Task Verify_ASixthTryInTheMinute_Returns429_EvenWithTheRightCode()
    {
        var factory = Factory(checksPerMinute: 5);
        using var client = await RegisteredAsync(factory);
        var child = (await MeAsync(client)).Children[0].ChildId;
        var challenge = (await (await client.PostAsJsonAsync(ApiClientExtensions.SecurityCode, ChildScope(child))).Content.ReadFromJsonAsync<CodeChallengeModel>())!.Challenge;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var wrong = await client.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge, code = "000000" });
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        var sixth = await client.PostAsJsonAsync(ApiClientExtensions.VerifyCode, new { challenge, code = FixedCodeGenerator.Code });

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.Equal("code-attempts-used", await sixth.ProblemCodeAsync());
    }

    private LanternApiFactory Factory(int requestsPerMinute = 1000, int requestsPerHour = 1000, int checksPerMinute = 1000)
    {
        var factory = new LanternApiFactory(
            azurite.ConnectionString,
            new Dictionary<string, string>
            {
                ["RateLimits:CodeRequestsPerMinute"] = requestsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["RateLimits:CodeRequestsPerHour"] = requestsPerHour.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["RateLimits:CodeChecksPerMinute"] = checksPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }
        );
        factories.Add(factory);

        return factory;
    }

    private static async Task<HttpClient> RegisteredAsync(LanternApiFactory factory)
    {
        var client = factory.CreateClient().WithBearer(TestTokens.Create($"uid-{Guid.NewGuid():N}"));
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
            ],
        };

    public void Dispose() => factories.ForEach(factory => factory.Dispose());
}
