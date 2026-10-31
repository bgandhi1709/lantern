using System.ComponentModel.DataAnnotations;

namespace Lantern.Core.Configuration;

public sealed class ActionOptions
{
    public const string SectionName = "Actions";

    [Required]
    public string Exchange { get; set; } = string.Empty;

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan ResendAfter { get; set; }
}
