using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Lantern.Api.Tests.Infrastructure;

internal static class ApiClientExtensions
{
    public static readonly Uri Register = new("/v1/register", UriKind.Relative);
    public static readonly Uri Me = new("/v1/me", UriKind.Relative);

    public static readonly Uri Family = new("/v1/family", UriKind.Relative);

    public static readonly Uri Children = new("/v1/family/children", UriKind.Relative);

    public static readonly Uri SecurityCode = new("/v1/security/code", UriKind.Relative);

    public static readonly Uri VerifyCode = new("/v1/security/code/verify", UriKind.Relative);

    public static Uri Child(Guid childId) => new($"/v1/family/children/{childId}", UriKind.Relative);

    public const string CodeProofHeader = "Lantern-Code-Proof";

    public static async Task<HttpResponseMessage> DeleteChildAsync(this HttpClient client, Guid childId)
    {
        var requested = await client.PostAsJsonAsync(SecurityCode, new { operation = "delete-child", target = childId });

        if (!requested.IsSuccessStatusCode)
        {
            return requested;
        }

        var challenge = (await requested.Content.ReadFromJsonAsync<Lantern.Api.Models.CodeChallengeModel>())!.Challenge;
        var verified = await client.PostAsJsonAsync(VerifyCode, new { challenge, code = FixedCodeGenerator.Code });

        return verified.IsSuccessStatusCode
            ? await client.DeleteChildAsync(childId, (await verified.Content.ReadFromJsonAsync<Lantern.Api.Models.CodeProofModel>())!.Proof)
            : verified;
    }

    public static async Task<string> ProofAsync(this HttpClient client, object scope)
    {
        var requested = await client.PostAsJsonAsync(SecurityCode, scope);
        var challenge = (await requested.Content.ReadFromJsonAsync<Lantern.Api.Models.CodeChallengeModel>())!.Challenge;
        var verified = await client.PostAsJsonAsync(VerifyCode, new { challenge, code = FixedCodeGenerator.Code });

        return (await verified.Content.ReadFromJsonAsync<Lantern.Api.Models.CodeProofModel>())!.Proof;
    }

    public static Task<HttpResponseMessage> DeleteChildAsync(this HttpClient client, Guid childId, string proof)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, Child(childId));

        if (proof is not null)
        {
            request.Headers.Add(CodeProofHeader, proof);
        }

        return client.SendAsync(request);
    }

    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static Task<HttpResponseMessage> RegisterAsync<TBody>(this HttpClient client, TBody body) =>
        client.PostAsJsonAsync(Register, body);

    public static async Task<string> ProblemCodeAsync(this HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return problem.GetProperty("code").GetString()!;
    }
}
