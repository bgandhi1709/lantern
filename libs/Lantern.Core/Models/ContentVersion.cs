namespace Lantern.Core.Models;

public sealed class ContentVersion
{
    public string Board { get; set; }

    public int Class { get; set; }

    public List<string> Books { get; set; } = [];

    public int Files { get; set; }

    public DateTimeOffset CopiedAt { get; set; }
}
