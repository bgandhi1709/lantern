using Lantern.Api.Contracts;
using Lantern.Api.Exceptions;
using Lantern.Api.Validation;

namespace Lantern.Api.Tests.Validation;

public sealed class ValidatorTests
{
    private const int Year = 2026;

    private static readonly ChildValidator Child = new(new FixedClock());

    [Theory]
    [InlineData(Year - 18)]
    [InlineData(Year - 10)]
    [InlineData(Year - 3)]
    public void Child_BirthYearInRange_Passes(int birthYear) => Child.Validate(new ChildDetailsBody { BirthYear = birthYear });

    [Theory]
    [InlineData(Year - 19)]
    [InlineData(Year - 2)]
    [InlineData(0)]
    public void Child_BirthYearOutOfRange_Throws(int birthYear) =>
        Assert.Throws<InvalidRegistrationException>(() => Child.Validate(new ChildDetailsBody { BirthYear = birthYear }));

    [Fact]
    public void AddChild_EmptyId_ThrowsAndAValidIdDefersToTheChildRules()
    {
        var add = new AddChildValidator(Child);

        Assert.Throws<InvalidRegistrationException>(() => add.Validate(new AddChildBody { BirthYear = Year - 8 }));
        Assert.Throws<InvalidRegistrationException>(() => add.Validate(new AddChildBody { ChildId = Guid.NewGuid(), BirthYear = Year - 30 }));
        add.Validate(new AddChildBody { ChildId = Guid.NewGuid(), BirthYear = Year - 8 });
    }

    [Fact]
    public void Registration_RequiresConsentAndChecksEveryChild()
    {
        var registration = new RegistrationValidator(Child);
        RegisterBody Body(bool accepted, int birthYear) =>
            new()
            {
                Consent = new ConsentBody { Accepted = accepted },
                Children = [new ChildBody { BirthYear = Year - 8 }, new ChildBody { BirthYear = birthYear }],
            };

        Assert.Throws<InvalidRegistrationException>(() => registration.Validate(Body(false, Year - 8)));
        Assert.Throws<InvalidRegistrationException>(() => registration.Validate(Body(true, Year - 30)));
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
