namespace Lantern.Core.Service;

public interface IValidator<in T>
{
    /// <exception cref="Exceptions.LanternException">(<c>InvalidRequest</c>) The instance breaks a rule the API model's attributes can't express.</exception>
    void Validate(T instance);
}
