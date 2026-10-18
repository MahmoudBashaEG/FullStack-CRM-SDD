using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(CrmDbContext dbContext) : IUnitOfWork
{
    // Only unique indexes that encode a business rule a concurrent user can legitimately hit. Any other unique violation is a
    // defect (or an Identity/infrastructure problem) and must surface as a logged 500, never as a misleading 409.
    private static readonly IReadOnlyDictionary<string, string> KnownUniqueIndexes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["UX_TicketCollaborators_ActiveMembership"] = "This Agent is already a collaborator.",
    };

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A row version (Tickets.Version, AgentTasks.Version) changed since this request loaded the row; nothing was saved.
            throw new ConflictException("The record was changed by another request. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (TryGetKnownUniqueConflict(exception, out var message))
        {
            throw new ConflictException(message); // nothing was saved
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        // Nested transactional command: join the outer transaction; the outer call commits.
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        // Required when EnableRetryOnFailure is on: one attempt = cleared tracker + transaction + WHOLE handler + save.
        // Attempts are retried only up to COMMIT; the commit itself is never replayed.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async ct =>
            {
                // Entities accepted by a failed attempt's SaveChanges were rolled back in the database: never reuse them.
                dbContext.ChangeTracker.Clear();

                await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
                TResult result;
                try
                {
                    result = await operation(ct);
                    await SaveChangesAsync(ct); // mapped save (409 rules); usually a no-op after the handler's own save
                }
                catch
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                    }
                    catch
                    {
                        // Connection already broken: SQL Server rolls the transaction back itself. Keep the original exception.
                    }

                    dbContext.ChangeTracker.Clear();
                    throw; // transient ⇒ the strategy re-runs this lambda from the top; anything else surfaces unchanged
                }

                try
                {
                    // Not cancellable: a client disconnect must not turn a commit into an unknown outcome.
                    await transaction.CommitAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    // The commit may have been applied. Never replay the handler: surface a NON-transient exception (503).
                    dbContext.ChangeTracker.Clear();
                    throw new CommitOutcomeUnknownException(exception);
                }

                return result;
            },
            cancellationToken);
    }

    private static bool TryGetKnownUniqueConflict(DbUpdateException exception, out string message)
    {
        message = string.Empty;
        if (exception.InnerException is not SqlException { Number: 2601 or 2627 } sql)
        {
            return false;
        }

        foreach (var (index, text) in KnownUniqueIndexes)
        {
            if (sql.Message.Contains($"'{index}'", StringComparison.Ordinal)) // SQL Server: "… with unique index 'UX_…'"
            {
                message = text;
                return true;
            }
        }

        return false;
    }
}
