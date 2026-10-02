using System.ComponentModel.DataAnnotations;

namespace Lantern.Core.Configuration;

// Set by the deployment from the queue it creates. The Functions trigger reads the same key as %Actions:Queue%.
public sealed class ActionOptions
{
    public const string SectionName = "Actions";

    [Required]
    public string Queue { get; set; } = string.Empty;

    // How long an action may go unsent before the API resends it on start: long enough not to resend one sent seconds ago.
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan ResendAfter { get; set; }
}
