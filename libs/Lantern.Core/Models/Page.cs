namespace Lantern.Core.Models;

public sealed class Page<T>(IReadOnlyList<T> items, string nextCursor)
{
    public IReadOnlyList<T> Items { get; } = items;

    public string NextCursor { get; } = nextCursor;
}
