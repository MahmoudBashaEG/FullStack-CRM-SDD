using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Portal.GetTicket;

public sealed record GetPortalTicketQuery(Guid UserId, Guid Id) : IQuery<PortalTicketDetails>;

public sealed class GetPortalTicketQueryHandler(ICustomerRepository customers, IPortalTicketRepository portal)
    : IQueryHandler<GetPortalTicketQuery, PortalTicketDetails>
{
    public async ValueTask<PortalTicketDetails> Handle(GetPortalTicketQuery query, CancellationToken cancellationToken)
    {
        var customerId = await customers.ResolveIdAsync(query.UserId, cancellationToken);
        return await portal.FindAsync(query.Id, customerId, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{query.Id}' was not found.");
    }
}
