using Crm.Application.Abstractions.Persistence;
using System.Linq.Expressions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Application.Sla;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class TicketRepository(CrmDbContext dbContext, SlaEvaluator slaEvaluator, TimeProvider timeProvider) : ITicketRepository
{
    public void Add(Ticket ticket) => dbContext.Tickets.Add(ticket);

    public Task<Ticket?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) =>
        Scope(dbContext.Tickets, agentScope).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<TicketDetails?> FindDetailsByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default)
    {
        var row = await Project(Scope(dbContext.Tickets.AsNoTracking(), agentScope).Where(t => t.Id == id)).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (await EnrichAsync([row], cancellationToken))[0];
    }

    public async Task<PagedResult<TicketDetails>> ListAsync(
        TicketListFilter filter, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Scope(dbContext.Tickets.AsNoTracking(), agentScope);

        if (filter.CustomerId is { } customerId)
        {
            query = query.Where(t => t.CustomerId == customerId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (filter.Category is { } category)
        {
            query = query.Where(t => t.Category == category);
        }

        if (filter.Priority is { } priority)
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (filter.Escalated is { } escalated)
        {
            query = query.Where(t => t.IsEscalated == escalated);
        }

        if (filter.SlaBreached is { } slaBreached)
        {
            var now = timeProvider.GetUtcNow(); // SQL-side comparison against the stored due instants
            var breached = IsBreached(now);
            query = query.Where(slaBreached
                ? breached
                : Expression.Lambda<Func<Ticket, bool>>(Expression.Not(breached.Body), breached.Parameters));
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            // Parameterized; EF Core escapes LIKE wildcards for string.Contains. A full GUID also matches the ticket id.
            var search = filter.Search;
            query = Guid.TryParse(search, out var ticketId)
                ? query.Where(t => t.Id == ticketId || t.Subject.Contains(search))
                : query.Where(t => t.Subject.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await Project(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketDetails>(await EnrichAsync(rows, cancellationToken), page, pageSize, totalCount);
    }

    // Applied first, in SQL: an Agent can never see or edit a ticket assigned to someone else or unassigned. Also used by AgentDashboardRepository.
    internal static IQueryable<Ticket> Scope(IQueryable<Ticket> tickets, Guid? agentScope) =>
        agentScope is { } agentId ? tickets.Where(t => t.AssignedAgentUserId == agentId) : tickets;

    // At least one stored SLA target breached: completed late, or still open and past due (stored instants only).
    private static Expression<Func<Ticket, bool>> IsBreached(DateTimeOffset now) => t =>
        (t.SlaFirstResponseDueAtUtc != null
            && ((t.FirstRespondedAtUtc == null && t.SlaFirstResponseDueAtUtc < now) || t.FirstRespondedAtUtc > t.SlaFirstResponseDueAtUtc))
        || (t.SlaResolutionDueAtUtc != null
            && ((t.SlaResolvedAtUtc == null && t.SlaResolutionDueAtUtc < now) || t.SlaResolvedAtUtc > t.SlaResolutionDueAtUtc));

    // The SLA view is computed after materialization, once per request, from the raw stored columns (one shared SlaEvaluator).
    private async Task<IReadOnlyList<TicketDetails>> EnrichAsync(IReadOnlyList<TicketRow> rows, CancellationToken cancellationToken)
    {
        if (rows.All(r => r.SlaFirstResponseDueAtUtc is null))
        {
            return rows.Select(r => r.Details).ToList();
        }

        var context = await slaEvaluator.CreateContextAsync(cancellationToken);
        return rows
            .Select(r => r.SlaFirstResponseDueAtUtc is null
                ? r.Details
                : r.Details with
                {
                    Sla = SlaEvaluator.Evaluate(
                        new TicketSlaInput(
                            r.Details.Category, r.Details.Priority, r.Details.AssignedAgentUserId is not null,
                            new TicketSlaSnapshot(
                                r.Details.CreatedAtUtc, r.SlaTimeZoneId!, r.SlaFirstResponseTargetMinutes!.Value, r.SlaFirstResponseDueAtUtc.Value,
                                r.FirstRespondedAtUtc, r.FirstRespondedByName, r.SlaResolutionTargetMinutes!.Value, r.SlaResolutionDueAtUtc!.Value,
                                r.SlaResolvedAtUtc)),
                        context),
                })
            .ToList();
    }

    private sealed record TicketRow(
        TicketDetails Details,
        string? SlaTimeZoneId,
        int? SlaFirstResponseTargetMinutes,
        DateTimeOffset? SlaFirstResponseDueAtUtc,
        DateTimeOffset? FirstRespondedAtUtc,
        string? FirstRespondedByName,
        int? SlaResolutionTargetMinutes,
        DateTimeOffset? SlaResolutionDueAtUtc,
        DateTimeOffset? SlaResolvedAtUtc);

    // Display names are joined, not stored. Ordered here (newest first, Id tie-breaker) because EF cannot order
    // by members of the constructed record (same rule as IdentityService.Project). Inner joins never drop rows:
    // CustomerId and CreatedByUserId are required FKs. Responder / escalator / assignee are left joins.
    private IQueryable<TicketRow> Project(IQueryable<Ticket> tickets) =>
        from t in tickets
        join c in dbContext.Customers on t.CustomerId equals c.Id
        join creator in dbContext.Users on t.CreatedByUserId equals creator.Id
        from agent in dbContext.Users.Where(u => u.Id == t.AssignedAgentUserId).DefaultIfEmpty()
        from escalatedBy in dbContext.Users.Where(u => u.Id == t.EscalatedByUserId).DefaultIfEmpty()
        from responder in dbContext.Users.Where(u => u.Id == t.FirstRespondedByUserId).DefaultIfEmpty()
        orderby t.CreatedAtUtc descending, t.Id descending
        select new TicketRow(
            new TicketDetails(
            t.Id,
            t.Subject,
            t.Description,
            t.Category,
            t.Priority,
            t.Status,
            t.CustomerId,
            c.FirstName + " " + c.LastName,
            c.Email,
            t.CreatedByUserId,
            creator.FirstName + " " + creator.LastName,
            t.AssignedAgentUserId,
            agent == null ? null : agent.FirstName + " " + agent.LastName,
            t.CreatedAtUtc,
            t.UpdatedAtUtc,
            t.IsEscalated,
            t.EscalationReason,
            t.EscalatedAtUtc,
            t.EscalatedByUserId,
            escalatedBy == null ? null : escalatedBy.FirstName + " " + escalatedBy.LastName),
            t.SlaTimeZoneId,
            t.SlaFirstResponseTargetMinutes,
            t.SlaFirstResponseDueAtUtc,
            t.FirstRespondedAtUtc,
            responder == null ? null : responder.FirstName + " " + responder.LastName,
            t.SlaResolutionTargetMinutes,
            t.SlaResolutionDueAtUtc,
            t.SlaResolvedAtUtc);

    public async Task<PagedResult<TicketActivityDetails>?> ListActivityAsync(
        Guid ticketId, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var visibleTicket = Scope(dbContext.Tickets.AsNoTracking(), agentScope).Where(t => t.Id == ticketId);
        if (!await visibleTicket.AnyAsync(cancellationToken))
        {
            return null;
        }

        // The scope is part of every statement, so a reassignment between the two queries cannot leak events.
        var activities =
            from a in dbContext.TicketActivities.AsNoTracking()
            join t in visibleTicket on a.TicketId equals t.Id
            select a;

        var totalCount = await activities.CountAsync(cancellationToken);
        var items = await (
                from a in activities
                from actor in dbContext.Users.Where(u => u.Id == a.ActorUserId).DefaultIfEmpty() // null actor = Automation
                from fromAgent in dbContext.Users.Where(u => u.Id == a.FromAgentUserId).DefaultIfEmpty()
                from toAgent in dbContext.Users.Where(u => u.Id == a.ToAgentUserId).DefaultIfEmpty()
                orderby a.OccurredAtUtc descending, a.Id descending
                select new TicketActivityDetails(
                    a.Id,
                    a.Type,
                    a.OccurredAtUtc,
                    actor == null ? SlaRules.AutomationActorName : actor.FirstName + " " + actor.LastName,
                    a.Field,
                    a.OldValue,
                    a.NewValue,
                    fromAgent == null ? null : fromAgent.FirstName + " " + fromAgent.LastName,
                    toAgent == null ? null : toAgent.FirstName + " " + toAgent.LastName,
                    a.Reason,
                    a.ActorUserId == null))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketActivityDetails>(items, page, pageSize, totalCount);
    }
}
