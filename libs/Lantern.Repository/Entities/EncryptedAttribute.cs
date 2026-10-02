namespace Lantern.Repository.Entities;

/// <summary>
/// The column is stored encrypted with the Family key. The name is part of the cipher's authenticated data, so it
/// stays fixed in code; renaming it makes every stored value unreadable.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class EncryptedAttribute(string column) : Attribute
{
    public string Column { get; } = column;
}
