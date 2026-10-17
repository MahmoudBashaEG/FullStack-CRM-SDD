using Crm.Domain.Customers;
using Crm.Domain.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Customers;

/// <summary>
/// Idempotent: creates one linked CRM profile for every Customer-role account that has none
/// (accounts created before CUST-001). Never touches Agents/SuperAdmin, passwords, roles or tokens.
/// </summary>
internal sealed class CustomerProfileInitializer(
    CrmDbContext dbContext,
    ILogger<CustomerProfileInitializer> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogWarning(
                "Customer profile initialization skipped: {Count} pending migration(s). Run 'dotnet ef database update' (README) and restart.",
                pending.Count);
            return;
        }

        var missing = await (
                from u in dbContext.Users.AsNoTracking()
                join ur in dbContext.UserRoles on u.Id equals ur.UserId
                join r in dbContext.Roles on ur.RoleId equals r.Id
                where r.Name == UserRoles.Customer && !dbContext.Customers.Any(c => c.UserId == u.Id)
                select new { u.Id, u.FirstName, u.LastName, u.Email, u.PhoneNumber, u.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var u in missing)
        {
            dbContext.Customers.Add(Customer.CreateForAccount(
                Guid.CreateVersion7(), u.Id, u.FirstName, u.LastName, u.Email!, u.PhoneNumber, u.CreatedAtUtc));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken); // one implicit transaction: all or nothing
            logger.LogInformation("Customer profile initialization created {Count} profile(s).", missing.Count);
        }
        catch (DbUpdateException exception)
        {
            // Unique UserId index: another process created a profile first. Nothing was saved; the next start retries.
            logger.LogWarning(exception, "Customer profile initialization did not complete; it will retry on next start.");
        }
    }
}
