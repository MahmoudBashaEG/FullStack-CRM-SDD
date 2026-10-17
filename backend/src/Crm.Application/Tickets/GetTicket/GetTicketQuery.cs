using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Tickets.GetTicket;

public sealed record GetTicketQuery(TicketActor Actor, Guid Id) : IQuery<TicketDetails>;

public sealed class GetTicketQueryHandler(ITicketRepository tickets) : IQueryHandler<GetTicketQuery, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(GetTicketQuery query, CancellationToken cancellationToken) =>
        await tickets.FindDetailsByIdAsync(query.Id, query.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{query.Id}' was not found.");
}
