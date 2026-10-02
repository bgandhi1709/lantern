using System.Net.Http.Json;
using System.Text.Json;

namespace Lantern.Api.Test.Integration.E2E;

// ID tokens from the local Firebase Auth Emulator. The user is created with this exact uid, so the seeded dev Family's
// owners (dev-parent-1 and dev-parent-2) can be signed in as well.
internal static class EmulatorTokens
{
    private const string Project = "lantern-ai-bg1709";
    private const string Password = "local-password";

    public static async Task<string> MintAsync(string emulator, string uid)
    {
        using var http = new HttpClient { BaseAddress = new Uri(emulator) };
        var email = $"{uid}@lantern.local";

        // An existing user answers 400; that is fine, the sign-in below decides.
        using var create = new HttpRequestMessage(HttpMethod.Post, $"/identitytoolkit.googleapis.com/v1/projects/{Project}/accounts")
        {
            Content = JsonContent.Create(new { localId = uid, email, password = Password, displayName = uid, emailVerified = true }),
        };
        create.Headers.Add("Authorization", "Bearer owner");
        await http.SendAsync(create);

        var signIn = await http.PostAsJsonAsync(
            "/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=local",
            new { email, password = Password, returnSecureToken = true }
        );
        signIn.EnsureSuccessStatusCode();

        var payload = await signIn.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("idToken").GetString()!;
    }
}
