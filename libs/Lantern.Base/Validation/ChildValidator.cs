using Lantern.Core.Exceptions;
using Lantern.Core.Models;
using Lantern.Core.Service;

namespace Lantern.Base.Validation;

internal sealed class ChildValidator(TimeProvider clock) : IValidator<Child>
{
    private const int MinChildAge = 3;
    private const int MaxChildAge = 18;

    public void Validate(Child instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var currentYear = clock.GetUtcNow().Year;
        if (instance.BirthYear < currentYear - MaxChildAge || instance.BirthYear > currentYear - MinChildAge)
        {
            throw new InvalidRequestException(
                $"A child's birth year must be between {currentYear - MaxChildAge} and {currentYear - MinChildAge}."
            );
        }
    }
}
