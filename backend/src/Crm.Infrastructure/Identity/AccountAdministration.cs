using Crm.Application.Abstractions.Identity;
using Crm.Application.Common.Paging;
using Crm.Domain.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Identity;

internal sealed class AccountAdministration(CrmDbContext dbContext) : IAccountAdministration
{
    private sealed class StatusRow
    {
        public Guid UserId { get; init; }
        public bool IsActive { get; init; }
    }

    // Filter/order BEFORE projecting: EF cannot translate predicates over the constructed record.
    private IQueryable<UserAccount> Project(IQueryable<ApplicationUser> users) =>
        from u in users.AsNoTracking()
        let role = (from ur in dbContext.UserRoles
                    join r in dbContext.Roles on ur.RoleId equals r.Id
                    where ur.UserId == u.Id
                    select r.Name).FirstOrDefault()
        select new UserAccount(
            u.Id, u.Email!, u.FirstName, u.LastName, u.PhoneNumber, role ?? string.Empty, u.IsActive, u.SecurityStamp!, u.CreatedAtUtc);

    public async Task<PagedResult<UserAccount>> ListAsync(
        AccountListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var users = dbContext.Users.AsNoTracking();
        if (filter.Search is { } s)
        {
            // Parameterized; EF Core escapes LIKE wildcards for string.Contains.
            users = users.Where(u =>
                u.FirstName.Contains(s) ||
                u.LastName.Contains(s) ||
                (u.FirstName + " " + u.LastName).Contains(s) ||
                u.Email!.Contains(s));
        }

        if (filter.Role is { } role)
        {
            users = users.Where(u => dbContext.UserRoles.Any(ur => ur.UserId == u.Id
                && dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));
        }

        if (filter.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var items = await Project(users
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ThenBy(u => u.Email)
                .ThenBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);
        return new PagedResult<UserAccount>(items, page, pageSize, totalCount);
    }

    // UPDLOCK: also under READ_COMMITTED_SNAPSHOT, reads the latest COMMITTED row and blocks writers until commit.
    // HOLDLOCK: keeps the lock to the end of the transaction. Reads only.
    public async Task<LockedAccountRow?> LockAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        var rows = await dbContext.Database.SqlQuery<StatusRow>($"""
            SELECT [Id] AS [UserId], [IsActive]
            FROM [AspNetUsers] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            WHERE [Id] = {userId}
            """).ToListAsync(cancellationToken); // materialize as written: no EF subquery wrapping around the hinted statement
        return rows.SingleOrDefault() is { } row ? new LockedAccountRow(row.UserId, row.IsActive) : null;
    }

    public async Task<IReadOnlyList<LockedAccountRow>> LockSuperAdminsAsync(CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        var rows = await dbContext.Database.SqlQuery<StatusRow>($"""
            SELECT u.[Id] AS [UserId], u.[IsActive]
            FROM [AspNetUsers] u WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            WHERE u.[Id] IN (SELECT ur.[UserId] FROM [AspNetUserRoles] ur
                             INNER JOIN [AspNetRoles] r ON r.[Id] = ur.[RoleId]
                             WHERE r.[Name] = {UserRoles.SuperAdmin})
            """).ToListAsync(cancellationToken);
        return rows.Select(r => new LockedAccountRow(r.UserId, r.IsActive)).ToList();
    }

    private void EnsureTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("IAccountAdministration locks must be used inside an ITransactionalCommand.");
        }
    }
}
