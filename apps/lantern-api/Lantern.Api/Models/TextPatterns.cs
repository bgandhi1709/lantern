namespace Lantern.Api.Models;

internal static class TextPatterns
{
    // \A..\z rather than ^..$: in .NET, $ also matches before a trailing newline.
    public const string NoControlCharacters = @"\A[^\p{C}]+\z";
    public const string NoControlCharactersOrEmpty = @"\A[^\p{C}]*\z";
}
