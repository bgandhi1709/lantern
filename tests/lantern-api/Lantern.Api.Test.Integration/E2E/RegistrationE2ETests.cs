using System.Net;
using System.Net.Http.Json;
using Lantern.Api.Models;

namespace Lantern.Api.Test.Integration.E2E;

// Excluded from `dotnet test --filter-not-trait "Category=E2E"` (the build-test job, which has no Docker stack).
// Run with `--filter-trait "Category=E2E"` against the local Docker stack - see .github/workflows/api.yml's e2e-docker job.
[Trait("Category", "E2E")]
public sealed class RegistrationE2ETests(RegisteredFamilyFixture fixture) : IClassFixture<RegisteredFamilyFixture>
{
    private static readonly Uri MeUri = new("/v1/me", UriKind.Relative);
    private static readonly Uri RegisterUri = new("/v1/register", UriKind.Relative);

    [Fact]
    public void Register_CreatedTheFamily()
    {
        Assert.Equal(HttpStatusCode.Created, fixture.RegisterResponse.StatusCode);
        Assert.NotEqual(Guid.Empty, fixture.Family.FamilyId);
        Assert.Equal("Gujarat", fixture.Family.Region);
        Assert.Single(fixture.Family.Children);
    }

    [Fact]
    public async Task Me_RoundTripsTheSameFamily()
    {
        var response = await fixture.Client.GetAsync(MeUri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<FamilyModel>();
        Assert.Equal(fixture.Family.FamilyId, me!.FamilyId);
    }

    [Fact]
    public async Task Register_SameCallerAgain_ReturnsConflict()
    {
        var response = await fixture.Client.PostAsJsonAsync(RegisterUri, RegisteredFamilyFixture.ValidBody());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_ConsentNotAccepted_ReturnsBadRequest()
    {
        var body = RegisteredFamilyFixture.ValidBody();
        body.Consent.Accepted = false;

        var response = await fixture.Client.PostAsJsonAsync(RegisterUri, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
