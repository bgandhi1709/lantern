using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lantern.Api.Contracts;

namespace Lantern.Api.Test.Integration.E2E;

// Talks to the API in the local Docker stack (deploy/local) over HTTPS: no WebApplicationFactory, no Azurite client, no
// key wrapper. Signs in as a fresh Auth Emulator user and registers one family; the stack's volumes hold the rows,
// so there is nothing to clean up.
public sealed class RegisteredFamilyFixture : IAsyncLifetime
{
    private static readonly Uri RegisterUri = new("/v1/register", UriKind.Relative);

    public HttpClient Client { get; private set; } = null!;

    public HttpResponseMessage RegisterResponse { get; private set; } = null!;

    public FamilyResponse Family { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var idToken = await EmulatorTokens.MintAsync(RequireEnv("E2E_AUTH_EMULATOR"), $"e2e-{Guid.NewGuid():N}");

        Client = new HttpClient { BaseAddress = new Uri(RequireEnv("E2E_BASE_URL")) };
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        RegisterResponse = await Client.PostAsJsonAsync(RegisterUri, ValidBody());
        Family =
            await RegisterResponse.Content.ReadFromJsonAsync<FamilyResponse>()
            ?? throw new InvalidOperationException("Register did not return a family.");
    }

    public ValueTask DisposeAsync()
    {
        Client?.Dispose();

        return ValueTask.CompletedTask;
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

    private static string RequireEnv(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is required to run the E2E tests.");
}
