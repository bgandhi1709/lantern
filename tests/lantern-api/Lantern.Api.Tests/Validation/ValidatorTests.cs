using Lantern.Base.Validation;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;

namespace Lantern.Api.Tests.Validation;

public sealed class ValidatorTests
{
    private const int Year = 2026;

    private static readonly ChildValidator ChildRules = new(new FixedClock());

    [Theory]
    [InlineData(Year - 18)]
    [InlineData(Year - 10)]
    [InlineData(Year - 3)]
    public void Child_BirthYearInRange_Passes(int birthYear) => ChildRules.Validate(new Child { BirthYear = birthYear });

    [Theory]
    [InlineData(Year - 19)]
    [InlineData(Year - 2)]
    [InlineData(0)]
    public void Child_BirthYearOutOfRange_Throws(int birthYear) =>
        Assert.Throws<InvalidRequestException>(() => ChildRules.Validate(new Child { BirthYear = birthYear }));

    [Fact]
    public void Registration_RequiresConsentAndChecksEveryChild()
    {
        var registration = new RegistrationValidator(ChildRules);
        Registration Body(bool accepted, int birthYear) =>
            new()
            {
                ConsentAccepted = accepted,
                Children = [new Child { BirthYear = Year - 8 }, new Child { BirthYear = birthYear }],
            };

        Assert.Throws<InvalidRequestException>(() => registration.Validate(Body(false, Year - 8)));
        Assert.Throws<InvalidRequestException>(() => registration.Validate(Body(true, Year - 30)));
        registration.Validate(Body(true, Year - 9));
    }

    [Fact]
    public void Normalizer_TrimsNameAndTurnsABlankSchoolIntoNull()
    {
        var text = new ChildTextNormalizer();

        Assert.Equal("Aarav", text.Name("  Aarav "));
        Assert.Null(text.School("   "));
        Assert.Null(text.School(null));
        Assert.Equal("Green School", text.School(" Green School "));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Year, 6, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
