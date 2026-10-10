namespace Lantern.Api.Models;

public sealed class ParentModel
{
    public Guid ParentId { get; set; }

    public string Name { get; set; }

    public string Email { get; set; }

    public string Language { get; set; }

    public string ConsentVersion { get; set; }

    public DateTimeOffset ConsentAt { get; set; }
}
