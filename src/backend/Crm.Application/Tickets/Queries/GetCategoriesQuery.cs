using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using MediatR;

namespace Crm.Application.Tickets.Queries;

public sealed record GetCategoriesQuery : IRequest<IReadOnlyList<ReferenceChoice>>;
public sealed class GetCategoriesHandler(ITicketReferenceData references) : IRequestHandler<GetCategoriesQuery, IReadOnlyList<ReferenceChoice>>
{
    public Task<IReadOnlyList<ReferenceChoice>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken) => references.GetCategoriesAsync(cancellationToken);
}
