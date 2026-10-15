namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Application-facing persistence boundary. Implemented in Infrastructure over the
/// request-scoped CrmDbContext. Handlers stage changes, then save once.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>One atomic save (EF Core wraps a single SaveChanges in its own transaction).</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside one explicit database transaction, then saves
    /// and commits. Rolls back if the operation throws or is cancelled. Retried as a whole
    /// unit when the execution strategy retries. Joins an already-open transaction.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
