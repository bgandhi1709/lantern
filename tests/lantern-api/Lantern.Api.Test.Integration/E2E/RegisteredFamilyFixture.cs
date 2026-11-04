using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
using Lantern.Api.Models;
using Lantern.Core.Constants;

namespace Lantern.Api.Test.Integration.E2E;

// Talks to the API in the local Docker stack (deploy/local) over HTTPS: no WebApplicationFactory, no Azurite client, no
// key wrapper. Signs in as a fresh Auth Emulator user and registers one family; the stack's volumes hold the rows,
// so there is nothing to clean up.
public sealed class OutboxEmail
{
    public string Recipient { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

public sealed class RegisteredFamilyFixture : IAsyncLifetime
{
    private static readonly Uri RegisterUri = new("/v1/register", UriKind.Relative);
    private static readonly Uri SecurityCodeUri = new("/v1/security/code", UriKind.Relative);
    private static readonly Uri VerifyUri = new("/v1/security/code/verify", UriKind.Relative);

    public HttpClient Client { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public HttpResponseMessage RegisterResponse { get; private set; } = null!;

    public FamilyModel Family { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var uid = $"e2e-{Guid.NewGuid():N}";
        Email = $"{uid}@lantern.local";
        var idToken = await EmulatorTokens.MintAsync(RequireEnv("E2E_AUTH_EMULATOR"), uid);

        Client = new HttpClient { BaseAddress = new Uri(RequireEnv("E2E_BASE_URL")) };
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        RegisterResponse = await Client.PostAsJsonAsync(RegisterUri, ValidBody());
        Family =
            await RegisterResponse.Content.ReadFromJsonAsync<FamilyModel>()
            ?? throw new InvalidOperationException("Register did not return a family.");
    }

    public ValueTask DisposeAsync()
    {
        Client?.Dispose();

        return ValueTask.CompletedTask;
    }

    public static FamilyRegisterRequest ValidBody() =>
        new()
        {
            FamilyId = Guid.NewGuid(),
            Region = "Gujarat",
            Board = BoardType.Cbse,
            Language = "gu",
            ParentName = "e2e-locked-parent-name",
            ParentEmail = "e2e-locked-parent-email",
            RecoveryWrappedKey = "e2e-recovery-wrapped-key",
            RecoverySalt = "e2e-recovery-salt",
            Consent = new ConsentModel { Accepted = true, NoticeVersion = "e2e" },
            Children =
            [
                new ChildSaveModel
                {
                    Name = "e2e-locked-child-name",
                    ClassLevel = 1,
                    BirthYear = "e2e-locked-birth-year",
                    School = "e2e-locked-school",
                },
            ],
        };

    public async Task<HttpResponseMessage> DeleteChildWithCodeAsync(Guid childId) =>
        await DeleteChildAsync(childId, await ProofAsync(new { operation = "delete-child", target = childId }));

    public async Task<HttpResponseMessage> DeleteFamilyWithCodeAsync() =>
        await DeleteFamilyAsync(await ProofAsync(new { operation = "delete-family" }));

    public async Task<string> ProofAsync(object scope)
    {
        var requested = await Client.PostAsJsonAsync(SecurityCodeUri, scope);
        requested.EnsureSuccessStatusCode();
        var challenge = (await requested.Content.ReadFromJsonAsync<CodeChallengeModel>())!.Challenge;

        var verified = await Client.PostAsJsonAsync(VerifyUri, new { challenge, code = await EmailedCodeAsync() });
        verified.EnsureSuccessStatusCode();

        return (await verified.Content.ReadFromJsonAsync<CodeProofModel>())!.Proof;
    }

    public Task<HttpResponseMessage> DeleteFamilyAsync(string proof) =>
        SendDeleteAsync(new Uri("/v1/family", UriKind.Relative), proof);

    public Task<HttpResponseMessage> DeleteChildAsync(Guid childId, string proof) =>
        SendDeleteAsync(new Uri($"/v1/family/children/{childId}", UriKind.Relative), proof);

    private Task<HttpResponseMessage> SendDeleteAsync(Uri uri, string proof)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);

        if (proof is not null)
        {
            request.Headers.Add("Lantern-Code-Proof", proof);
        }

        return Client.SendAsync(request);
    }

    public async Task<string> EmailedCodeAsync()
    {
        var emails = await Client.GetFromJsonAsync<List<OutboxEmail>>(new Uri($"/dev/outbox?to={Uri.EscapeDataString(Email)}", UriKind.Relative));

        return Regex.Match(emails!.Last().Text, "[0-9]{6}").Value;
    }

    private static string RequireEnv(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is required to run the E2E tests.");
}
