using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Portal;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

/// <summary>PORTAL-001 (P4): customer ownership in every statement; only customer-safe columns are ever selected.</summary>
internal sealed class PortalTicketRepository(CrmDbContext dbContext) : IPortalTicketRepository
{
    public async Task<PagedResult<PortalTicketSummary>> ListAsync(
        Guid customerId, PortalTicketListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Owned(customerId);

        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            // Parameterized; EF Core escapes LIKE wildcards for string.Contains. A full GUID also matches the ticket id (as TicketRepository).
            var search = filter.Search;
            query = Guid.TryParse(search, out var ticketId)
                ? query.Where(t => t.Id == ticketId || t.Subject.Contains(search))
                : query.Where(t => t.Subject.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new PortalTicketSummary(t.Id, t.Subject, t.Category, t.Status, t.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<PortalTicketSummary>(items, page, pageSize, totalCount);
    }

    public Task<PortalTicketDetails?> FindAsync(Guid ticketId, Guid customerId, CancellationToken cancellationToken = default) =>
        (from t in Owned(customerId).Where(t => t.Id == ticketId)
         from f in dbContext.TicketFeedback.AsNoTracking().Where(f => f.TicketId == t.Id).DefaultIfEmpty()
         select new PortalTicketDetails(
             t.Id, t.Subject, t.Description, t.Category, t.Status, t.CreatedAtUtc,
             f == null ? null : new PortalFeedbackDetails(f.Rating, f.Comment, f.CreatedAtUtc, f.UpdatedAtUtc)))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PortalTicketHistory?> FindHistoryAsync(
        Guid ticketId, Guid customerId, int maxEvents, CancellationToken cancellationToken = default)
    {
        var ticket = Owned(customerId).Where(t => t.Id == ticketId);
        var createdAtUtc = await ticket.Select(t => (DateTimeOffset?)t.CreatedAtUtc).SingleOrDefaultAsync(cancellationToken);
        if (createdAtUtc is null)
        {
            return null;
        }

        // Ownership is part of this statement too. Only the safe columns are selected (P5).
        var newest = await (
                from a in dbContext.TicketActivities.AsNoTracking()
                join t in ticket on a.TicketId equals t.Id
                where a.Type == TicketActivityType.Created || a.Type == TicketActivityType.StatusChanged
                orderby a.OccurredAtUtc descending, a.Id descending
                select new PortalActivityEvent(a.Id, a.Type, a.OccurredAtUtc, a.OldValue, a.NewValue))
            .Take(maxEvents)
            .ToListAsync(cancellationToken);

        newest.Reverse(); // oldest first for display
        return new PortalTicketHistory(createdAtUtc.Value, newest);
    }

    public void AddFeedback(TicketFeedback feedback) => dbContext.TicketFeedback.Add(feedback);

    public Task<TicketFeedback?> GetFeedbackAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        dbContext.TicketFeedback.SingleOrDefaultAsync(f => f.TicketId == ticketId, cancellationToken);

    // Applied first, in SQL: a customer only ever reaches tickets of their own CRM profile. Staff scope (TicketRepository.Scope) is separate.
    private IQueryable<Ticket> Owned(Guid customerId) =>
        dbContext.Tickets.AsNoTracking().Where(t => t.CustomerId == customerId);
}
