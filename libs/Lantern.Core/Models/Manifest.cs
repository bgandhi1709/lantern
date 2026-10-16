namespace Lantern.Core.Models;

public abstract class Manifest
{
    public string Schema { get; set; }

    public void EnsureSchema(string expected)
    {
        if (Schema != expected)
        {
            throw new InvalidOperationException($"The {GetType().Name} has a schema this version does not know.");
        }
    }
}
