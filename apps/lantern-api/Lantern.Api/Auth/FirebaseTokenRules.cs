using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lantern.Api.Auth;

/// <summary>
/// Pure claim rules for an already-verified Firebase ID token. JwtBearer is responsible for
/// signature, issuer, audience and lifetime validation; these rules check the claims that are
/// specific to Firebase itself (uid shape, sign-in provider) and produce a reason code that is
/// safe to log because it never carries a claim value.
/// </summary>
public static partial class FirebaseTokenRules
{
    public const string SubjectClaim = "sub";
    public const string FirebaseClaim = "firebase";
    public const string GoogleProvider = "google.com";

    private const string SignInProviderProperty = "sign_in_provider";

    /// <summary>Null when the principal is acceptable; otherwise a short reason code that is safe to log.</summary>
    public static string? FindViolation(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var subject = principal.FindFirst(SubjectClaim);
        if (subject is null)
        {
            return "missing-subject";
        }

        if (!UidPattern().IsMatch(subject.Value))
        {
            return "malformed-subject";
        }

        var firebase = principal.FindFirst(FirebaseClaim);
        if (firebase is null)
        {
            return "missing-firebase-claim";
        }

        string? signInProvider;
        try
        {
            using var document = JsonDocument.Parse(firebase.Value);
            if (!document.RootElement.TryGetProperty(SignInProviderProperty, out var providerElement)
                || providerElement.ValueKind != JsonValueKind.String)
            {
                return "malformed-firebase-claim";
            }

            signInProvider = providerElement.GetString();
        }
        catch (JsonException)
        {
            return "malformed-firebase-claim";
        }

        if (!string.Equals(signInProvider, GoogleProvider, StringComparison.Ordinal))
        {
            return "sign-in-provider-not-allowed";
        }

        return null;
    }

    /// <summary>The Firebase uid. Throws InvalidOperationException when absent (only after FindViolation passed).</summary>
    public static string GetUid(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirst(SubjectClaim)?.Value
            ?? throw new InvalidOperationException("The principal has no subject claim.");
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$")]
    private static partial Regex UidPattern();
}
