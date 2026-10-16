namespace Lantern.Core.Models;

public sealed class FamilyManifest : Manifest
{
    public string Board { get; set; }

    public Dictionary<string, FamilyChild> Children { get; set; } = [];

    public static FamilyManifest Empty(string schema, string board) => new() { Schema = schema, Board = board };

    public IReadOnlyList<Subject> SubjectsOf(Guid childId) => Children.GetValueOrDefault(Key(childId))?.Subjects;

    public bool Has(Guid childId, FamilyChild entry) => Children.TryGetValue(Key(childId), out var stored) && stored.Is(entry);

    public void Add(Guid childId, FamilyChild entry) => Children[Key(childId)] = entry;

    public bool Remove(Guid childId) => Children.Remove(Key(childId));

    private static string Key(Guid childId) => childId.ToString("D");
}
