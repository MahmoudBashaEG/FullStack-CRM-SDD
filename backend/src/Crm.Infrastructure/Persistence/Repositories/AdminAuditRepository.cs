using Crm.Application.Abstractions.Persistence;
using Crm.Application.Administration;
using Crm.Application.Common.Paging;
using Crm.Domain.Administration;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class AdminAuditRepository(CrmDbContext dbContext) : IAdminAuditRepository
{
    public void Add(AdminAuditEntry entry) => dbContext.AdminAuditLogs.Add(entry);

    public async Task<PagedResult<AdminAuditEntryDetails>> ListAsync(
        AdminAuditAction? action, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, string? search,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AdminAuditLogs.AsNoTracking();
        if (action is { } a)
        {
            query = query.Where(e => e.Action == a);
        }

        if (fromUtc is { } from)
        {
            query = query.Where(e => e.OccurredAtUtc >= from);
        }

        if (toUtcExclusive is { } to)
        {
            query = query.Where(e => e.OccurredAtUtc < to);
        }

        if (search is not null)
        {
            if (Guid.TryParse(search, out var id))
            {
                var text = id.ToString();
                query = query.Where(e => e.ActorUserId == id || e.TargetId == text);
            }
            else
            {
                query = query.Where(e => e.TargetLabel.Contains(search)
                    || dbContext.Users.Any(u => u.Id == e.ActorUserId
                        && (u.Email!.Contains(search) || (u.FirstName + " " + u.LastName).Contains(search))));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Page the entities first, then look up actors for just that page (the converted Changes column is read through the entity).
        var entries = await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var actorIds = entries.Select(e => e.ActorUserId).Distinct().ToList();
        var actors = await dbContext.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var items = entries.Select(e =>
        {
            actors.TryGetValue(e.ActorUserId, out var actor);
            return new AdminAuditEntryDetails(
                e.Id, e.OccurredAtUtc, e.Action, e.ActorUserId, actor?.Name, actor?.Email, e.TargetType, e.TargetId, e.TargetLabel, e.Changes);
        }).ToList();

        return new PagedResult<AdminAuditEntryDetails>(items, page, pageSize, totalCount);
    }
}
