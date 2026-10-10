namespace Lantern.Api.Models;

public sealed class ChildModel
{
    public Guid ChildId { get; set; }

    public string NameLocked { get; set; } = string.Empty;

    public string? SchoolLocked { get; set; }

    public int ClassLevel { get; set; }

    public string BirthYearLocked { get; set; } = string.Empty;
}
