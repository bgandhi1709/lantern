using System.Buffers.Text;
using System.Text;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Identity;
using Lantern.Core.Models;
using Lantern.Core.Security;
using Lantern.Core.Service;

namespace Lantern.Base.Services;

internal sealed class OneTimeCodeService(
    IIdentityResolver identityResolver,
    ICryptoService crypto,
    IOneTimeCodeGenerator generator,
    IEmailService email,
    IJsonService json,
    TimeProvider clock
) : IOneTimeCodeService
{
    private const string ChallengePurpose = "code-challenge";
    private const string ProofPurpose = "code-proof";
    private const string Subject = "Your Lantern one-time code";
    private static readonly TimeSpan ChallengeLife = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ProofLife = TimeSpan.FromMinutes(5);

    public async Task<string> RequestAsync(string operation, string target, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var caller = identityResolver.Identity;

        if (!caller.EmailVerified || string.IsNullOrWhiteSpace(caller.Email))
        {
            throw new LanternException(LanternErrorCode.EmailNotVerified);
        }

        var code = generator.NewCode();
        var challenge = Challenge(operation, target, caller, code);

        await email.SendAsync(caller.Email, Subject, EmailText(code), cancellationToken);

        return challenge;
    }

    public string Verify(string challenge, string code)
    {
        var token = OpenChallenge(challenge, code, out var caller);

        if (token.ExpiresAt <= clock.GetUtcNow())
        {
            throw new LanternException(LanternErrorCode.CodeExpired);
        }

        return Seal(
            new CodeToken
            {
                Subject = crypto.Hash(caller.Uid),
                Operation = token.Operation,
                Target = token.Target,
                ExpiresAt = clock.GetUtcNow().Add(ProofLife),
                Nonce = generator.NewNonce(),
            },
            ProofPurpose
        );
    }

    public void RequireProof(string proof, string operation, string target)
    {
        var parts = (proof ?? string.Empty).Split('.');

        if (parts.Length != 2 || !crypto.Matches(ProofPurpose, parts[0], parts[1]))
        {
            throw new LanternException(LanternErrorCode.ProofRequired);
        }

        var token = Open(parts[0]);
        var valid =
            token is not null
            && token.Subject == crypto.Hash(identityResolver.Identity.Uid)
            && token.Operation == operation
            && token.Target == Normalized(target)
            && token.ExpiresAt > clock.GetUtcNow();

        if (!valid)
        {
            throw new LanternException(LanternErrorCode.ProofRequired);
        }
    }

    private CodeToken OpenChallenge(string challenge, string code, out CallerIdentity caller)
    {
        caller = identityResolver.Identity;
        var parts = (challenge ?? string.Empty).Split('.');

        if (parts.Length != 2 || !crypto.Matches(ChallengePurpose, $"{parts[0]}.{code}", parts[1]))
        {
            throw new LanternException(LanternErrorCode.CodeWrong);
        }

        var token = Open(parts[0]);

        return token is not null && token.Subject == crypto.Hash(caller.Uid) ? token : throw new LanternException(LanternErrorCode.CodeWrong);
    }

    private CodeToken Open(string payload) => json.Deserialize<CodeToken>(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(payload)));

    private string Challenge(string operation, string target, CallerIdentity caller, string code)
    {
        var payload = Payload(
            new CodeToken
            {
                Subject = crypto.Hash(caller.Uid),
                Operation = operation,
                Target = Normalized(target),
                ExpiresAt = clock.GetUtcNow().Add(ChallengeLife),
                Nonce = generator.NewNonce(),
            }
        );

        return $"{payload}.{crypto.Sign(ChallengePurpose, $"{payload}.{code}")}";
    }

    private string Seal(CodeToken token, string purpose)
    {
        var payload = Payload(token);

        return $"{payload}.{crypto.Sign(purpose, payload)}";
    }

    private string Payload(CodeToken token) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json.Serialize(token)));

    private static string Normalized(string target) => target ?? string.Empty;

    private static string EmailText(string code) =>
        $"Your Lantern code is {code}. It works for 5 minutes. If you did not ask for it, ignore this email and nothing will change.";
}
