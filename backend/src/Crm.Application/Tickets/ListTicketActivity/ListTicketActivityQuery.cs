using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Mediator;

namespace Crm.Application.Tickets.ListTicketActivity;

public sealed record ListTicketActivityQuery(TicketActor Actor, Guid TicketId, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<TicketActivityDetails>>;

public sealed class ListTicketActivityQueryHandler(ITicketRepository tickets)
    : IQueryHandler<ListTicketActivityQuery, PagedResult<TicketActivityDetails>>
{
    public async ValueTask<PagedResult<TicketActivityDetails>> Handle(ListTicketActivityQuery query, CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize); // documented cap
        // Same visibility as the ticket: a former assignee gets 404 immediately after reassignment.
        return await tickets.ListActivityAsync(query.TicketId, query.Actor.AgentScope, query.Page, pageSize, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{query.TicketId}' was not found.");
    }
}
