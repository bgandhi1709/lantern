using Lantern.Base.Validation;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;

namespace Lantern.Api.Tests.Validation;

public sealed class ValidatorTests
{
    private static readonly RegistrationValidator Rules = new();

    private static Registration Body(bool accepted = true, BoardType board = BoardType.Cbse, int classLevel = 3, Guid? familyId = null) =>
        new()
        {
            FamilyId = familyId ?? Guid.NewGuid(),
            ConsentAccepted = accepted,
            Board = board,
            Children = [new Child { ClassLevel = 1 }, new Child { ClassLevel = classLevel }],
        };

    [Fact]
    public void Registration_WithConsentAndAFamilyId_Passes() => Rules.Validate(Body());

    [Fact]
    public void Registration_WithoutConsent_Throws() => Assert.Throws<InvalidRequestException>(() => Rules.Validate(Body(accepted: false)));

    [Fact]
    public void Registration_WithoutAFamilyId_Throws() => Assert.Throws<InvalidRequestException>(() => Rules.Validate(Body(familyId: Guid.Empty)));

    [Theory]
    [InlineData(BoardType.Cbse, 10, true)]
    [InlineData(BoardType.Ssc, 5, true)]
    [InlineData(BoardType.Ssc, 6, false)]
    [InlineData(BoardType.Ssc, 10, false)]
    public void Registration_ChecksEveryChildsClassAgainstTheBoard(BoardType board, int classLevel, bool allowed)
    {
        void Act() => Rules.Validate(Body(board: board, classLevel: classLevel));

        if (allowed)
        {
            Act();
        }
        else
        {
            Assert.Throws<ClassNotAvailableException>(Act);
        }
    }

    [Theory]
    [InlineData(BoardType.Cbse, 0, false)]
    [InlineData(BoardType.Cbse, 11, false)]
    [InlineData(BoardType.Ssc, 1, true)]
    public void Child_ClassAvailability(BoardType board, int classLevel, bool expected) =>
        Assert.Equal(expected, Child.IsClassAvailable(board, classLevel));
}
