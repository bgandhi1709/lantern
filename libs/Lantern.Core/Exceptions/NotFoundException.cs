namespace Lantern.Core.Exceptions;

public sealed class NotFoundException(string resource) : Exception($"No such {resource} in this family.")
{
    public string Resource { get; } = resource;
}
