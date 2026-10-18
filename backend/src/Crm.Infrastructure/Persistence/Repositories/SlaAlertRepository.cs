using Crm.Application.Abstractions.Persistence;
using Crm.Application.AgentTasks;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.SlaAlerts;
using Crm.Domain.Automation;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class SlaAlertRepository(CrmDbContext dbContext) : ISlaAlertRepository
{
    public Task<int> CountUnreadAsync(StaffActor actor, CancellationToken cancellationToken = default) =>
        Visible(actor).CountAsync(a => a.ReadAtUtc == null, cancellationToken);

    public async Task<PagedResult<SlaAlertDetails>> ListAsync(
        StaffActor actor, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Visible(actor);
        if (unreadOnly)
        {
            query = query.Where(a => a.ReadAtUtc == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await (
                from a in query
                join t in dbContext.Tickets on a.TicketId equals t.Id
                orderby a.CreatedAtUtc descending, a.Id descending
                select new SlaAlertDetails(a.Id, t.Id, t.Subject, t.Status, a.Type, a.Kind, a.DueAtUtc, a.CreatedAtUtc, a.ReadAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<SlaAlertDetails>(items, page, pageSize, totalCount);
    }

    // One conditional UPDATE evaluated at execution time with the same visibility predicate as the list; explicit strategy (R2) around an
    // idempotent autocommit statement (a replay after a lost acknowledgement affects 0 rows => AlreadyRead).
    public async Task<MarkReadOutcome> MarkReadAsync(long id, StaffActor actor, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var userId = actor.UserId;
        var isSuperAdmin = actor.IsSuperAdmin;
        var updated = await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            ct => dbContext.SlaAlerts
                .Where(a => a.Id == id && a.ReadAtUtc == null && a.RecipientUserId == userId
                            && (isSuperAdmin || dbContext.Tickets.Any(t => t.Id == a.TicketId && t.AssignedAgentUserId == userId)))
                .ExecuteUpdateAsync(set => set.SetProperty(a => a.ReadAtUtc, now), ct),
            cancellationToken);
        if (updated == 1)
        {
            return MarkReadOutcome.MarkedRead;
        }

        return await Visible(actor).AnyAsync(a => a.Id == id, cancellationToken) ? MarkReadOutcome.AlreadyRead : MarkReadOutcome.NotFound;
    }

    // Recipient AND current access: SuperAdmin => any ticket; Agent => ticket currently assigned to them (same rule as TicketRepository.Scope).
    private IQueryable<SlaAlert> Visible(StaffActor actor)
    {
        var userId = actor.UserId;
        var isSuperAdmin = actor.IsSuperAdmin;
        return dbContext.SlaAlerts.AsNoTracking()
            .Where(a => a.RecipientUserId == userId
                        && (isSuperAdmin || dbContext.Tickets.Any(t => t.Id == a.TicketId && t.AssignedAgentUserId == userId)));
    }
}
