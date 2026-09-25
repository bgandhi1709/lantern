using System.Security.Claims;
using Lantern.Api.Auth;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Lantern.Api.Tests.Auth;

public sealed class FirebaseTokenRulesTests
{
    private const string GoogleFirebaseClaim = "{\"sign_in_provider\":\"google.com\"}";

    [Theory]
    [InlineData(null, GoogleFirebaseClaim, "missing-subject")]
    [InlineData("not a valid uid!", GoogleFirebaseClaim, "malformed-subject")]
    [InlineData("abc123", null, "missing-firebase-claim")]
    [InlineData("abc123", "not-json", "malformed-firebase-claim")]
    [InlineData("abc123", "{\"other\":1}", "malformed-firebase-claim")]
    [InlineData("abc123", "{\"sign_in_provider\":123}", "malformed-firebase-claim")]
    [InlineData("abc123", "42", "malformed-firebase-claim")]
    [InlineData("abc123", "[\"google.com\"]", "malformed-firebase-claim")]
    public void FindViolation_InvalidClaims_ReturnsReasonCode(string? sub, string? firebase, string expectedReason)
    {
        var principal = CreatePrincipal(sub, firebase);

        var reason = FirebaseTokenRules.FindViolation(principal);

        Assert.Equal(expectedReason, reason);
    }

    [Fact]
    public void FindViolation_GoogleProvider_ReturnsNull()
    {
        var principal = CreatePrincipal("abc123", GoogleFirebaseClaim);

        var reason = FirebaseTokenRules.FindViolation(principal);

        Assert.Null(reason);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("password")]
    [InlineData("phone")]
    public void FindViolation_NonGoogleProvider_ReturnsSignInProviderNotAllowed(string provider)
    {
        var principal = CreatePrincipal("abc123", $"{{\"sign_in_provider\":\"{provider}\"}}");

        var reason = FirebaseTokenRules.FindViolation(principal);

        Assert.Equal("sign-in-provider-not-allowed", reason);
    }

    [Fact]
    public void GetUid_SubjectPresent_ReturnsValue()
    {
        var principal = CreatePrincipal("abc123", GoogleFirebaseClaim);

        var uid = FirebaseTokenRules.GetUid(principal);

        Assert.Equal("abc123", uid);
    }

    [Fact]
    public void GetUid_SubjectMissing_Throws()
    {
        var principal = CreatePrincipal(null, GoogleFirebaseClaim);

        Assert.Throws<InvalidOperationException>(() => FirebaseTokenRules.GetUid(principal));
    }

    private static ClaimsPrincipal CreatePrincipal(string? sub, string? firebase)
    {
        var claims = new List<Claim>();
        if (sub is not null)
        {
            claims.Add(new Claim(FirebaseTokenRules.SubjectClaim, sub));
        }

        if (firebase is not null)
        {
            claims.Add(new Claim(FirebaseTokenRules.FirebaseClaim, firebase, JsonClaimValueTypes.Json));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }
}
