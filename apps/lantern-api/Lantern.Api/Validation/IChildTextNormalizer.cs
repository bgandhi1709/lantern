namespace Lantern.Api.Validation;

public interface IChildTextNormalizer
{
    string Name(string name);

    string? School(string? school);
}

internal sealed class ChildTextNormalizer : IChildTextNormalizer
{
    public string Name(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return name.Trim();
    }

    public string? School(string? school) => string.IsNullOrWhiteSpace(school) ? null : school.Trim();
}
