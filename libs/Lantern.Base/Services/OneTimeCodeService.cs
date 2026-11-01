using System.Globalization;
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
    TimeProvider clock
) : IOneTimeCodeService
{
    private const string ChallengePurpose = "code-challenge";
    private const string ProofPurpose = "code-proof";
    private const string EmailSubject = "Your Lantern one-time code";
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
        var challenge = crypto.Seal(ChallengePurpose, TokenFor(operation, target ?? string.Empty, ChallengeLife), code);

        await email.SendAsync(caller.Email, EmailSubject, EmailText(code), cancellationToken);

        return challenge;
    }

    public string Verify(string challenge, string code)
    {
        var token = crypto.Open<CodeToken>(ChallengePurpose, challenge, code);

        if (token is null || token.UidHash != CallerHash)
        {
            throw new LanternException(LanternErrorCode.CodeWrong);
        }

        if (token.ExpiresAt <= clock.GetUtcNow())
        {
            throw new LanternException(LanternErrorCode.CodeExpired);
        }

        return crypto.Seal(ProofPurpose, TokenFor(token.Operation, token.Target, ProofLife), string.Empty);
    }

    public void RequireProof(string proof, string operation, string target)
    {
        var token = crypto.Open<CodeToken>(ProofPurpose, proof, string.Empty);
        var valid =
            token is not null
            && token.UidHash == CallerHash
            && token.Operation == operation
            && token.Target == target
            && token.ExpiresAt > clock.GetUtcNow();

        if (!valid)
        {
            throw new LanternException(LanternErrorCode.ProofRequired);
        }
    }

    private string CallerHash => crypto.Hash(identityResolver.Identity.Uid);

    private CodeToken TokenFor(string operation, string target, TimeSpan life) =>
        new()
        {
            UidHash = CallerHash,
            Operation = operation,
            Target = target,
            ExpiresAt = clock.GetUtcNow().Add(life),
            Nonce = generator.NewNonce(),
        };

    private static string EmailText(string code) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Your Lantern code is {code}. It works for {ChallengeLife.TotalMinutes:0} minutes. If you did not ask for it, ignore this email and nothing will change."
        );
}
