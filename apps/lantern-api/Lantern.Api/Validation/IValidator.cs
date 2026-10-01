namespace Lantern.Api.Validation;

public interface IValidator<in T>
{
    /// <exception cref="Exceptions.InvalidRegistrationException">The instance breaks a rule the attributes can't express.</exception>
    void Validate(T instance);
}
