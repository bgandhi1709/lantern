using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Identity;
using Lantern.Api.Contracts;

namespace Lantern.Api.Tests.E2E;

// Talks to the real, deployed UAT API over HTTPS: no WebApplicationFactory, no Azurite, no local
// key wrapper. Registers one real family through a real Firebase-issued ID token, and deletes it
// again in DisposeAsync regardless of whether the tests passed.
//
// The ID token comes from the GitHub Actions job's own OIDC identity, not a service account: the
// runner mints a short-lived token for this job (ACTIONS_ID_TOKEN_REQUEST_URL/TOKEN, present
// whenever the job has `permissions: id-token: write`) and Firebase's "GitHub Actions" OpenID
// Connect provider (Identity Platform, trusting token.actions.githubusercontent.com) exchanges it
// directly for a Firebase ID token. No key material exists anywhere - see infra/README.md.
public sealed class RegisteredFamilyFixture : IAsyncLifetime
{
    // Must match the OIDC provider's Client ID in the Firebase console exactly - see
    // infra/README.md's E2E setup section.
    private const string GitHubOidcAudience = "lantern-e2e";
    private const string FirebaseOidcProviderId = "oidc.github-actions";

    // Any of the project's default-authorized domains works; the REST API rejects an
    // unauthorized one, it doesn't need to be reachable.
    private const string FirebaseAuthorizedDomain = "https://lantern-ai-bg1709.firebaseapp.com";

    private static readonly Uri RegisterUri = new("/v1/register", UriKind.Relative);

    public HttpClient Client { get; private set; } = null!;

    public HttpResponseMessage RegisterResponse { get; private set; } = null!;

    public FamilyResponse Family { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var idToken = await MintIdTokenAsync();

        Client = new HttpClient { BaseAddress = new Uri(RequireEnv("E2E_BASE_URL")) };
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        RegisterResponse = await Client.PostAsJsonAsync(RegisterUri, ValidBody());
        Family =
            await RegisterResponse.Content.ReadFromJsonAsync<FamilyResponse>()
            ?? throw new InvalidOperationException("Register did not return a family.");
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();

        var tableEndpoint = Environment.GetEnvironmentVariable("E2E_STORAGE_TABLE_ENDPOINT");
        if (Family is null || string.IsNullOrEmpty(tableEndpoint))
        {
            return;
        }

        var endpoint = new Uri(tableEndpoint);
        var credential = new DefaultAzureCredential();
        var parents = new TableClient(
            endpoint,
            Environment.GetEnvironmentVariable("E2E_PARENTS_TABLE") ?? "parents",
            credential
        );
        var families = new TableClient(
            endpoint,
            Environment.GetEnvironmentVariable("E2E_FAMILIES_TABLE") ?? "families",
            credential
        );

        // FamilyId is the only plaintext lookup key on the profile row - everything else is
        // per-family encrypted, so it's the only thing cleanup can filter by.
        await foreach (
            var profile in parents.QueryAsync<TableEntity>(TableClient.CreateQueryFilter($"FamilyId eq {Family.FamilyId}"))
        )
        {
            await parents.DeleteEntityAsync(profile.PartitionKey, profile.RowKey);
        }

        var familyPartition = Family.FamilyId.ToString("D");
        await foreach (var entity in families.QueryAsync<TableEntity>(row => row.PartitionKey == familyPartition))
        {
            await families.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
        }
    }

    public static RegisterBody ValidBody() =>
        new()
        {
            Region = "Gujarat",
            Language = "gu",
            Consent = new ConsentBody { Accepted = true, NoticeVersion = "e2e" },
            Children =
            [
                new ChildBody
                {
                    Name = "E2E Child",
                    ClassLevel = 1,
                    BirthYear = DateTime.UtcNow.Year - 6,
                },
            ],
        };

    private static async Task<string> MintIdTokenAsync()
    {
        var githubToken = await RequestGitHubOidcTokenAsync();

        using var http = new HttpClient();
        var exchange = await http.PostAsJsonAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={RequireEnv("FIREBASE_WEB_API_KEY")}",
            new
            {
                postBody = $"id_token={githubToken}&providerId={FirebaseOidcProviderId}",
                requestUri = FirebaseAuthorizedDomain,
                returnSecureToken = true,
            }
        );
        exchange.EnsureSuccessStatusCode();

        var payload = await exchange.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("idToken").GetString()!;
    }

    private static async Task<string> RequestGitHubOidcTokenAsync()
    {
        var requestUrl = RequireEnv("ACTIONS_ID_TOKEN_REQUEST_URL");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "bearer",
            RequireEnv("ACTIONS_ID_TOKEN_REQUEST_TOKEN")
        );

        var response = await http.GetAsync($"{requestUrl}&audience={GitHubOidcAudience}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("value").GetString()!;
    }

    private static string RequireEnv(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is required to run the E2E tests.");
}
