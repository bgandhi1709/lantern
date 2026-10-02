namespace Lantern.Core.Service;

public interface IValidator<in T>
{
    /// <exception cref="Exceptions.InvalidRequestException">The instance breaks a rule the API model's attributes can't express.</exception>
    void Validate(T instance);
}
