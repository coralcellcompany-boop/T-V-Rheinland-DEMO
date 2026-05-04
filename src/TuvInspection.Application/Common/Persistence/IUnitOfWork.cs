namespace TuvInspection.Application.Common.Persistence;

/// <summary>
/// Commits all pending domain changes in a single transaction, including outbox enqueues
/// and audit log rows. Resolved per-request from the DI container.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChanges(CancellationToken ct);
}
