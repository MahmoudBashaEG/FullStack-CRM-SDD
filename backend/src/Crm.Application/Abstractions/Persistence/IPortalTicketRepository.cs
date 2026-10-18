using Crm.Application.Common.Paging;
using Crm.Application.Portal;
using Crm.Domain.Tickets;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// PORTAL-001 customer scope (P4): every read filters Tickets.CustomerId == customerId in SQL, in every statement.
/// Separate from ITicketRepository (staff scope), which is unchanged. Not owned or missing ⇒ null.
/// AddFeedback only stages; the caller saves through IUnitOfWork.
/// </summary>
public interface IPortalTicketRepository
{
    /// <summary>Ordered CreatedAtUtc DESC, Id DESC.</summary>
    Task<PagedResult<PortalTicketSummary>> ListAsync(
        Guid customerId, PortalTicketListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PortalTicketDetails?> FindAsync(Guid ticketId, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Created + StatusChanged events only, the newest maxEvents, returned oldest first (OccurredAtUtc, Id).</summary>
    Task<PortalTicketHistory?> FindHistoryAsync(Guid ticketId, Guid customerId, int maxEvents, CancellationToken cancellationToken = default);

    void AddFeedback(TicketFeedback feedback);

    /// <summary>Tracked, for updates. The caller has already verified ownership.</summary>
    Task<TicketFeedback?> GetFeedbackAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
