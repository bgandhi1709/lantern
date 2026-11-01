namespace Lantern.Core.Security;

public interface ICryptoService
{
    /// <summary>The keyed, deterministic hash of a uid, used to find a Parent before any Family is known.</summary>
    string Hash(string uid);

    string Seal(string purpose, object payload, string binding);

    T Open<T>(string purpose, string token, string binding)
        where T : class;
}
