namespace Lantern.Core.Models;

public sealed class Registration
{
    public string Region { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public bool ConsentAccepted { get; set; }

    public string ConsentNoticeVersion { get; set; } = string.Empty;

    public IReadOnlyList<Child> Children { get; set; } = [];
}
