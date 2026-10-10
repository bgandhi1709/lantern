using Lantern.Core.Constants;

namespace Lantern.Core.Exceptions;

/// <summary>A failure the caller must see. <see cref="Code"/> says which; the message is developer-authored and safe to return.</summary>
public sealed class LanternException(LanternErrorCode code, string message = null) : Exception(message ?? code.ToString())
{
    public LanternErrorCode Code { get; } = code;
}
