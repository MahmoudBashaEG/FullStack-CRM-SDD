using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Mediator;

namespace Crm.Application.Portal.GetTimeline;

public sealed record GetPortalTicketTimelineQuery(Guid UserId, Guid Id) : IQuery<IReadOnlyList<PortalTimelineEntry>>;

public sealed class GetPortalTicketTimelineQueryHandler(ICustomerRepository customers, IPortalTicketRepository portal)
    : IQueryHandler<GetPortalTicketTimelineQuery, IReadOnlyList<PortalTimelineEntry>>
{
    public async ValueTask<IReadOnlyList<PortalTimelineEntry>> Handle(GetPortalTicketTimelineQuery query, CancellationToken cancellationToken)
    {
        var customerId = await customers.ResolveIdAsync(query.UserId, cancellationToken);
        var history = await portal.FindHistoryAsync(query.Id, customerId, PortalRules.MaxTimelineEvents, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{query.Id}' was not found.");
        return PortalTimeline.Build(history);
    }
}
