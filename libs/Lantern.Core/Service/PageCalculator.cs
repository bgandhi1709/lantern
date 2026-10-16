using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Lantern.Core.Models;

namespace Lantern.Core.Service;

internal sealed class PageCalculator : IPageCalculator
{
    private const int DefaultLimit = 50;

    private const int MaxLimit = 100;

    private const int MaxCursorLength = 16;

    public Page<T> Slice<T>(IReadOnlyList<T> all, int? limit, string cursor)
    {
        ArgumentNullException.ThrowIfNull(all);

        var size = limit ?? DefaultLimit;
        if (size is < 1 or > MaxLimit)
        {
            throw new LanternException(LanternErrorCode.InvalidPage);
        }

        var start = string.IsNullOrEmpty(cursor) ? 0 : Decode(cursor, all.Count);
        var next = start + size;

        return new Page<T>([.. all.Skip(start).Take(size)], next < all.Count ? Encode(next) : null);
    }

    private static string Encode(int offset) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(offset.ToString(CultureInfo.InvariantCulture)));

    private static int Decode(string cursor, int count) =>
        cursor.Length <= MaxCursorLength
        && Base64Url.IsValid(cursor)
        && int.TryParse(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)), NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
        && offset >= 1
        && offset < count
            ? offset
            : throw new LanternException(LanternErrorCode.InvalidPage);
}
