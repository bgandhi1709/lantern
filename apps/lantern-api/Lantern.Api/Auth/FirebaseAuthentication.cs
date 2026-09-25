using Lantern.Api.Configuration;
using Lantern.Api.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Lantern.Api.Auth;

public static class FirebaseAuthentication
{
    // Google-only sign-in is enforced in the Firebase console. A revoked or disabled user stays valid until their ID token expires (1 hour at most).
    public static IServiceCollection AddFirebaseAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<FirebaseOptions>, ILoggerFactory>(
                (options, firebase, loggerFactory) =>
                {
                    var projectId = firebase.Value.ProjectId;
                    var issuer = $"https://securetoken.google.com/{projectId}";
                    var logger = loggerFactory.CreateLogger("Lantern.Api.Auth");

                    options.Authority = issuer;
                    options.MapInboundClaims = false;
                    options.IncludeErrorDetails = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = projectId,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.FromMinutes(2),
                    };
                    options.Events = new JwtBearerEvents
                    {
                        // Exception type only: the message can quote token contents.
                        OnAuthenticationFailed = context =>
                        {
                            Log.TokenInvalid(logger, context.Exception.GetType().Name);
                            return Task.CompletedTask;
                        },
                    };
                }
            );

        services.AddAuthorizationBuilder().SetFallbackPolicy(
            new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build()
        );

        return services;
    }
}
