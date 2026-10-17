using Crm.Application.Abstractions.Persistence;
using Crm.Application.AgentDashboard;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class AgentDashboardRepository(CrmDbContext dbContext) : IAgentDashboardRepository
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
        Guid agentUserId, int limit, CancellationToken cancellationToken = default) =>
        await (
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
                select new AttentionTicket(
                    t.Id,
                    t.Subject,
                    t.Category,
                    t.Priority,
                    t.Status,
                    t.IsEscalated,
                    t.CustomerId,
                    c.FirstName + " " + c.LastName,
                    c.Email,
                    t.UpdatedAtUtc))
            .Take(limit)
            .ToListAsync(cancellationToken);

    // The one ownership filter shared with ticket list/detail/activity, then "needs attention" = Open or InProgress.
    private IQueryable<Ticket> ActiveAssignedTo(Guid agentUserId) =>
        TicketRepository.Scope(dbContext.Tickets.AsNoTracking(), agentUserId)
            .Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress);
}
