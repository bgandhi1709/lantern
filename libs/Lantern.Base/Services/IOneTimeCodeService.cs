namespace Lantern.Base.Services;

public interface IOneTimeCodeService
{
    Task<string> RequestAsync(string operation, string target, CancellationToken cancellationToken);

    string Verify(string challenge, string code);

    void RequireProof(string proof, string operation, string target);
}
