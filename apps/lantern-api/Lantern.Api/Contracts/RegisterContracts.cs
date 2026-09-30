using System.ComponentModel.DataAnnotations;
using Lantern.Api.Models;

namespace Lantern.Api.Contracts;

public sealed class RegisterBody
{
    private const int MaxChildren = 6;

    [Required]
    [StringLength(60, MinimumLength = 1)]
    [RegularExpression(TextRules.NoControlCharacters)]
    public string Region { get; set; } = string.Empty;

    [Required]
    [AllowedValues("en", "gu", "hi")]
    public string Language { get; set; } = string.Empty;

    [Required]
    public ConsentBody Consent { get; set; } = new();

    [Required]
    [MinLength(1)]
    [MaxLength(MaxChildren)]
    public List<ChildBody> Children { get; set; } = [];
}

public sealed class ConsentBody
{
    public bool Accepted { get; set; }

    [Required]
    [StringLength(20, MinimumLength = 1)]
    [RegularExpression(TextRules.NoControlCharacters)]
    public string NoticeVersion { get; set; } = string.Empty;
}

public sealed class ChildBody
{
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression(TextRules.NoControlCharacters)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 10)]
    public int ClassLevel { get; set; }

    public int BirthYear { get; set; }

    [StringLength(120)]
    [RegularExpression(TextRules.NoControlCharactersOrEmpty)]
    public string? School { get; set; }
}

internal static class TextRules
{
    // \A..\z rather than ^..$: in .NET, $ also matches before a trailing newline.
    public const string NoControlCharacters = @"\A[^\p{C}]+\z";
    public const string NoControlCharactersOrEmpty = @"\A[^\p{C}]*\z";
}

public sealed class FamilyResponse
{
    public Guid FamilyId { get; set; }

    public string Region { get; set; } = string.Empty;

    public ParentResponse Parent { get; set; } = new();

    public List<ChildResponse> Children { get; set; } = [];

    public static FamilyResponse From(FamilyView family)
    {
        ArgumentNullException.ThrowIfNull(family);

        return new FamilyResponse
        {
            FamilyId = family.FamilyId,
            Region = family.Region,
            Parent = ParentResponse.From(family.Parent),
            Children = [.. family.Children.Select(ChildResponse.From)],
        };
    }
}

public sealed class ParentResponse
{
    public Guid ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string ConsentVersion { get; set; } = string.Empty;

    public DateTimeOffset ConsentAt { get; set; }

    public static ParentResponse From(ParentView parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        return new ParentResponse
        {
            ParentId = parent.ParentId,
            Name = parent.Name,
            Email = parent.Email,
            Language = parent.Language,
            ConsentVersion = parent.ConsentVersion,
            ConsentAt = parent.ConsentAt,
        };
    }
}

public sealed class ChildResponse
{
    public Guid ChildId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? School { get; set; }

    public int ClassLevel { get; set; }

    public int BirthYear { get; set; }

    public static ChildResponse From(ChildView child)
    {
        ArgumentNullException.ThrowIfNull(child);

        return new ChildResponse
        {
            ChildId = child.ChildId,
            Name = child.Name,
            School = child.School,
            ClassLevel = child.ClassLevel,
            BirthYear = child.BirthYear,
        };
    }
}
