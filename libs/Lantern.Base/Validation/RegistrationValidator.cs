using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Service;

namespace Lantern.Base.Validation;

internal sealed class RegistrationValidator : IValidator<Registration>
{
    public void Validate(Registration instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!instance.ConsentAccepted)
        {
            throw new InvalidRequestException("Consent must be accepted to register.");
        }

        if (instance.FamilyId == Guid.Empty)
        {
            throw new InvalidRequestException("A family id is required.");
        }

        if (instance.Children.Any(child => !Child.IsClassAvailable(instance.Board, child.ClassLevel)))
        {
            throw new ClassNotAvailableException();
        }
    }
}
