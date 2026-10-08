using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using MediatR;

namespace Crm.Application.Tickets.Queries;

public sealed record GetEligibleAgentsQuery : IRequest<IReadOnlyList<ReferenceChoice>>;
public sealed class GetEligibleAgentsHandler(ITicketReferenceData references) : IRequestHandler<GetEligibleAgentsQuery, IReadOnlyList<ReferenceChoice>>
{
    public Task<IReadOnlyList<ReferenceChoice>> Handle(GetEligibleAgentsQuery request, CancellationToken cancellationToken) => references.GetAgentsAsync(cancellationToken);
}
