namespace Lantern.Api.Models;

public sealed class PageModel<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];

    public string NextCursor { get; set; }
}
