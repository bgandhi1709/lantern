using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Configuration;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    [Range(1, 10_000)]
    public int ChildrenPerMinute { get; set; }

    [Range(1, 10_000)]
    public int CodeRequestsPerMinute { get; set; }

    [Range(1, 10_000)]
    public int CodeRequestsPerHour { get; set; }

    [Range(1, 10_000)]
    public int CodeChecksPerMinute { get; set; }
}
