using Lantern.Core.Constants;
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
            throw new LanternException(LanternErrorCode.InvalidRequest, "Consent must be accepted to register.");
        }

        if (!Enum.IsDefined(instance.Board))
        {
            throw new LanternException(LanternErrorCode.InvalidRequest, "A Board is required.");
        }

        if (instance.FamilyId == Guid.Empty)
        {
            throw new LanternException(LanternErrorCode.InvalidRequest, "A family id is required.");
        }

        if (instance.Children.Any(child => !Child.IsClassAvailable(instance.Board, child.ClassLevel)))
        {
            throw new LanternException(LanternErrorCode.ClassNotAvailable);
        }
    }
}
