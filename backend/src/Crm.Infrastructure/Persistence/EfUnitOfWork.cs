using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(CrmDbContext dbContext) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A row version (Tickets.Version) changed since this request loaded the row; nothing was saved.
            throw new ConflictException("The record was changed by another request. Reload it and try again.");
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

        // Required when EnableRetryOnFailure is on: the WHOLE transaction is the retry unit.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async ct =>
            {
                // A retry must not replay change-tracker state from the failed attempt.
                dbContext.ChangeTracker.Clear();

                await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
                try
                {
                    var result = await operation(ct);
                    await dbContext.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            },
            cancellationToken);
    }
}
