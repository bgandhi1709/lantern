using System.Globalization;
using System.Security.Cryptography;

namespace Lantern.Core.Service;

internal sealed class OneTimeCodeGenerator : IOneTimeCodeGenerator
{
    private const int CodeSpace = 1_000_000;
    private const int NonceBytes = 16;

    public string NewCode() => RandomNumberGenerator.GetInt32(CodeSpace).ToString("D6", CultureInfo.InvariantCulture);

    public string NewNonce() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(NonceBytes));
}
