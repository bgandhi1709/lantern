using Lantern.Api.Contracts;
using Lantern.Api.Exceptions;

namespace Lantern.Api.Validation;

internal sealed class RegistrationValidator(IValidator<ChildDetailsBody> child) : IValidator<RegisterBody>
{
    public void Validate(RegisterBody instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!instance.Consent.Accepted)
        {
            throw new InvalidRegistrationException("Consent must be accepted to register.");
        }

        foreach (var entry in instance.Children)
        {
            child.Validate(entry);
        }
    }
}
