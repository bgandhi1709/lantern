using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Configuration;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    [Range(1, 10_000)]
    public int ChildrenPerMinute { get; set; }
}
