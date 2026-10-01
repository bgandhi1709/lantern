using Lantern.Api.Contracts;
using Lantern.Api.Exceptions;

namespace Lantern.Api.Validation;

internal sealed class ChildValidator(TimeProvider clock) : IValidator<ChildDetailsBody>
{
    private const int MinChildAge = 3;
    private const int MaxChildAge = 18;

    public void Validate(ChildDetailsBody instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var currentYear = clock.GetUtcNow().Year;
        if (instance.BirthYear < currentYear - MaxChildAge || instance.BirthYear > currentYear - MinChildAge)
        {
            throw new InvalidRegistrationException(
                $"A child's birth year must be between {currentYear - MaxChildAge} and {currentYear - MinChildAge}."
            );
        }
    }
}

internal sealed class AddChildValidator(IValidator<ChildDetailsBody> child) : IValidator<AddChildBody>
{
    public void Validate(AddChildBody instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance.ChildId == Guid.Empty)
        {
            throw new InvalidRegistrationException("A child id is required.");
        }

        child.Validate(instance);
    }
}
