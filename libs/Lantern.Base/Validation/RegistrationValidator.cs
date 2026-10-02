using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Service;

namespace Lantern.Base.Validation;

internal sealed class RegistrationValidator(IValidator<Child> child) : IValidator<Registration>
{
    public void Validate(Registration instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!instance.ConsentAccepted)
        {
            throw new InvalidRequestException("Consent must be accepted to register.");
        }

        foreach (var entry in instance.Children)
        {
            child.Validate(entry);
        }
    }
}
