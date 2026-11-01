namespace Lantern.Api.Models;

internal static class TextPatterns
{
    // \A..\z rather than ^..$: in .NET, $ also matches before a trailing newline.
    public const string NoControlCharacters = @"\A[^\p{C}]+\z";
    public const string KebabName = @"\A[a-z]+(-[a-z]+)*\z";
    public const string SixDigits = @"\A[0-9]{6}\z";
    public const string NoControlCharactersOrEmpty = @"\A[^\p{C}]*\z";
}
