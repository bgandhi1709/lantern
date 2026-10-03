using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Lantern.Api.Tests.Infrastructure;

internal static class ApiClientExtensions
{
    public static readonly Uri Register = new("/v1/register", UriKind.Relative);
    public static readonly Uri Me = new("/v1/me", UriKind.Relative);

    public static readonly Uri Family = new("/v1/family", UriKind.Relative);

    public static readonly Uri Children = new("/v1/family/children", UriKind.Relative);

    public static Uri Child(Guid childId) => new($"/v1/family/children/{childId}", UriKind.Relative);

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
