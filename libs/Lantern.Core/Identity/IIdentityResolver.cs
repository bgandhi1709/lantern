namespace Lantern.Core.Identity;

public interface IIdentityResolver
{
    /// <exception cref="Exceptions.CallerNotIdentifiedException">The verified token has no subject.</exception>
    CallerIdentity Identity { get; }
}
