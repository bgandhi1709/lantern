namespace Lantern.Core.Security;

public interface ICryptoService
{
    /// <summary>The keyed, deterministic hash of a uid, used to find a Parent before any Family is known.</summary>
    string Hash(string uid);
}
