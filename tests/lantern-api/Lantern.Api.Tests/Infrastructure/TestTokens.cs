using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Lantern.Api.Tests.Infrastructure;

// The factory trusts only this key, so the real JwtBearer checks run on every test request.
public static class TestTokens
{
    public const string ProjectId = "lantern-test";

    public static RsaSecurityKey Key { get; } = NewKey("test-key");

    public static RsaSecurityKey OtherKey { get; } = NewKey("other-key");

    public static string Create(
        string uid,
        string name = null,
        string email = null,
        string projectId = ProjectId,
        string audience = null,
        string issuer = null,
        DateTimeOffset? expires = null,
        SecurityKey key = null
    )
    {
        var expiry = expires ?? DateTimeOffset.UtcNow.AddMinutes(30);
        var claims = new Dictionary<string, object> { ["sub"] = uid };

        if (name is not null)
        {
            claims["name"] = name;
        }

        if (email is not null)
        {
            claims["email"] = email;
        }

        return new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = issuer ?? $"https://securetoken.google.com/{projectId}",
                Audience = audience ?? projectId,
                IssuedAt = expiry.UtcDateTime.AddHours(-1),
                NotBefore = expiry.UtcDateTime.AddHours(-1),
                Expires = expiry.UtcDateTime,
                Claims = claims,
                SigningCredentials = new SigningCredentials(key ?? Key, SecurityAlgorithms.RsaSha256),
            }
        );
    }

    private static RsaSecurityKey NewKey(string keyId) => new(RSA.Create(2048)) { KeyId = keyId };
}
