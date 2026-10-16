using Lantern.Core.Models;

namespace Lantern.Core.Service;

public interface IPageCalculator
{
    Page<T> Slice<T>(IReadOnlyList<T> all, int? limit, string cursor);
}
