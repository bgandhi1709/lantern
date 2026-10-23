namespace Lantern.Core.Models;

public sealed class Chapter : IEquatable<Chapter>
{
    public Chapter(string chapterId, string bookId, int number, string title, string summary)
    {
        ChapterId = chapterId;
        BookId = bookId;
        Number = number;
        Title = title;
        Summary = summary;
    }

    public string ChapterId { get; }

    public string BookId { get; }

    public int Number { get; }

    public string Title { get; }

    public string Summary { get; }

    public Chapter WithNumber(int number) => new(ChapterId, BookId, number, Title, Summary);

    public bool Equals(Chapter other) =>
        other is not null
        && ChapterId == other.ChapterId
        && BookId == other.BookId
        && Number == other.Number
        && Title == other.Title
        && Summary == other.Summary;

    public override bool Equals(object obj) => Equals(obj as Chapter);

    public override int GetHashCode() => HashCode.Combine(ChapterId, BookId, Number, Title, Summary);
}
