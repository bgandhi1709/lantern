using Lantern.Api.Exceptions;

namespace Lantern.Api.Services;

internal static class ChildRules
{
    private const int MinChildAge = 3;
    private const int MaxChildAge = 18;

    public static void ValidateBirthYear(int birthYear, int currentYear)
    {
        if (birthYear < currentYear - MaxChildAge || birthYear > currentYear - MinChildAge)
        {
            throw new InvalidRegistrationException(
                $"A child's birth year must be between {currentYear - MaxChildAge} and {currentYear - MinChildAge}."
            );
        }
    }

    public static string? CleanSchool(string? school) => string.IsNullOrWhiteSpace(school) ? null : school.Trim();
}
