namespace Lantern.Api.Repository;

public sealed record PendingDelete(Guid FamilyId, Guid ChildId, int Attempts);

public interface IChildDeletionStore
{
    /// <summary>Records the Parent's intent and hides the child. Safe to repeat.</summary>
    /// <exception cref="Exceptions.ChildNotFoundException">The family has no such child.</exception>
    Task RequestAsync(Guid familyId, Guid childId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Takes the next due job under a lease, or null when none is due.</summary>
    Task<PendingDelete?> ClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task RemoveChildRowAsync(PendingDelete job, CancellationToken cancellationToken);

    /// <summary>The last step: once this runs the job is done.</summary>
    Task RemovePendingAsync(PendingDelete job, CancellationToken cancellationToken);
}
