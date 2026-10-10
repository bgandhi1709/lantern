namespace Lantern.Api.Models;

public sealed class ParentModel
{
    public Guid ParentId { get; set; }

    public string NameLocked { get; set; } = string.Empty;

    public string EmailLocked { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string ConsentVersion { get; set; } = string.Empty;

    public DateTimeOffset ConsentAt { get; set; }
}
