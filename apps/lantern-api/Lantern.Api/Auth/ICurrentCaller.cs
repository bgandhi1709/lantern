namespace Lantern.Api.Auth;

public interface ICurrentCaller
{
    /// <exception cref="Exceptions.CallerNotIdentifiedException">The verified token has no subject.</exception>
    Caller Require();
}
