namespace Lantern.Api.Models;

public sealed class ChildModel
{
    public Guid ChildId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? School { get; set; }

    public int ClassLevel { get; set; }

    public int BirthYear { get; set; }
}
