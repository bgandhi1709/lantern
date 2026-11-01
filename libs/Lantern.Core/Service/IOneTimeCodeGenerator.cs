namespace Lantern.Core.Service;

public interface IOneTimeCodeGenerator
{
    string NewCode();

    string NewNonce();
}
