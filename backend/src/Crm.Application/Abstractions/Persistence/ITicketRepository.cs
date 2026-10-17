using Crm.Application.Common.Paging;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Add only stages; the caller saves through IUnitOfWork.
/// agentScope: null = no restriction (SuperAdmin); otherwise only tickets whose AssignedAgentUserId equals it.
/// The scope is applied in SQL before any other filter.
/// </summary>
public interface ITicketRepository
{
    void Add(Ticket ticket);

    /// <summary>Tracked, for updates. Null when missing or outside the scope.</summary>
    Task<Ticket?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default);

    Task<TicketDetails?> FindDetailsByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default);

    /// <summary>Ordered CreatedAtUtc DESC, Id DESC.</summary>
    Task<PagedResult<TicketDetails>> ListAsync(
        TicketListFilter filter, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default);
}
