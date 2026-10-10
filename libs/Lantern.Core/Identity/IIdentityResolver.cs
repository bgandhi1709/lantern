namespace Lantern.Core.Identity;

public interface IIdentityResolver
{
    /// <exception cref="Core.Exceptions.LanternException">(<c>CallerNotIdentified</c>) The verified token has no subject.</exception>
    CallerIdentity Identity { get; }
}
