using Crm.Application.Abstractions.Persistence;
using Crm.Application.AgentDashboard;
using Crm.Application.Sla;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class AgentDashboardRepository(CrmDbContext dbContext, SlaEvaluator slaEvaluator) : IAgentDashboardRepository
{
    public async Task<AgentWorkloadCounts> GetWorkloadCountsAsync(Guid agentUserId, CancellationToken cancellationToken = default) =>
        await ActiveAssignedTo(agentUserId)
            .GroupBy(_ => 1) // single group => one COUNT(CASE WHEN ...) statement
            .Select(g => new AgentWorkloadCounts(
                g.Count(t => t.Status == TicketStatus.Open),
                g.Count(t => t.Status == TicketStatus.InProgress),
                g.Count(t => t.IsEscalated),
                g.Count(t => t.Priority == TicketPriority.High || t.Priority == TicketPriority.Urgent)))
            .SingleOrDefaultAsync(cancellationToken)
        ?? new AgentWorkloadCounts(0, 0, 0, 0);

    public async Task<IReadOnlyList<AttentionTicket>> ListAttentionQueueAsync(
        Guid agentUserId, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from t in ActiveAssignedTo(agentUserId)
                join c in dbContext.Customers on t.CustomerId equals c.Id
                // Priority is stored as a name: rank explicitly, never alphabetically.
                orderby t.IsEscalated ? 0 : 1,
                    t.Priority == TicketPriority.Urgent ? 0
                    : t.Priority == TicketPriority.High ? 1
                    : t.Priority == TicketPriority.Medium ? 2
                    : 3,
                    t.UpdatedAtUtc,
                    t.Id
                select new
                {
                    t.Id,
                    t.Subject,
                    t.Category,
                    t.Priority,
                    t.Status,
                    t.IsEscalated,
                    t.CustomerId,
                    CustomerName = c.FirstName + " " + c.LastName,
                    CustomerEmail = c.Email,
                    t.UpdatedAtUtc,
                    t.CreatedAtUtc,
                    t.SlaTimeZoneId,
                    t.SlaFirstResponseTargetMinutes,
                    t.SlaFirstResponseDueAtUtc,
                    t.FirstRespondedAtUtc,
                    t.SlaResolutionTargetMinutes,
                    t.SlaResolutionDueAtUtc,
                    t.SlaResolvedAtUtc,
                })
            .Take(limit)
            .ToListAsync(cancellationToken);

        // Ordering, limit and the workload counts are unchanged; the SLA indicator is added after materialization (shared evaluator).
        var context = rows.Any(r => r.SlaFirstResponseDueAtUtc is not null) ? await slaEvaluator.CreateContextAsync(cancellationToken) : null;
        return rows
            .Select(r => new AttentionTicket(
                r.Id, r.Subject, r.Category, r.Priority, r.Status, r.IsEscalated, r.CustomerId, r.CustomerName, r.CustomerEmail, r.UpdatedAtUtc,
                r.SlaFirstResponseDueAtUtc is null || context is null
                    ? SlaIndicator.NotTracked
                    : SlaEvaluator.Evaluate(
                        new TicketSlaInput(
                            r.Category, r.Priority, IsAssigned: true,
                            new TicketSlaSnapshot(
                                r.CreatedAtUtc, r.SlaTimeZoneId!, r.SlaFirstResponseTargetMinutes!.Value, r.SlaFirstResponseDueAtUtc.Value,
                                r.FirstRespondedAtUtc, null, r.SlaResolutionTargetMinutes!.Value, r.SlaResolutionDueAtUtc!.Value, r.SlaResolvedAtUtc)),
                        context).Indicator))
            .ToList();
    }

    // The one ownership filter shared with ticket list/detail/activity, then "needs attention" = Open or InProgress.
    private IQueryable<Ticket> ActiveAssignedTo(Guid agentUserId) =>
        TicketRepository.Scope(dbContext.Tickets.AsNoTracking(), agentUserId)
            .Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress);
}
