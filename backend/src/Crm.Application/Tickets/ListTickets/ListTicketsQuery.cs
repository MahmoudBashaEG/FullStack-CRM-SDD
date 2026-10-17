using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Domain.Tickets;
using Mediator;

namespace Crm.Application.Tickets.ListTickets;

public sealed record ListTicketsQuery(
    TicketActor Actor,
    string? Search,
    Guid? CustomerId,
    TicketStatus? Status,
    TicketCategory? Category,
    TicketPriority? Priority,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<TicketDetails>>;

public sealed class ListTicketsQueryHandler(ITicketRepository tickets)
    : IQueryHandler<ListTicketsQuery, PagedResult<TicketDetails>>
{
    public async ValueTask<PagedResult<TicketDetails>> Handle(ListTicketsQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize); // documented cap: larger values are clamped
        var filter = new TicketListFilter(search, query.CustomerId, query.Status, query.Category, query.Priority);

        // Requested filters only ever narrow the actor's scope; they can never widen it.
        return await tickets.ListAsync(filter, query.Actor.AgentScope, query.Page, pageSize, cancellationToken);
    }
}
