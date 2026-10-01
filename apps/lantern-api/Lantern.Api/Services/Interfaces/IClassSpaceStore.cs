namespace Lantern.Api.Services.Interfaces;

public interface IClassSpaceStore
{
    /// <summary>Creates the Class space on first call; a repeated Class reuses it and changes nothing.</summary>
    Task StartAsync(Guid familyId, Guid childId, int classLevel, CancellationToken cancellationToken);

    /// <summary>Removes every Class space of the Child. Safe to repeat.</summary>
    Task DeleteChildAsync(Guid familyId, Guid childId, CancellationToken cancellationToken);
}
