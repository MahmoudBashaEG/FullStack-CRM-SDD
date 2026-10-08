using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using MediatR;
namespace Crm.Application.Tickets.Queries;

public sealed record GetTicketQuery(string Reference) : IRequest<OperationResult<TicketDetailDto>>;
public sealed class GetTicketHandler(ITicketReadService reader) : IRequestHandler<GetTicketQuery, OperationResult<TicketDetailDto>>
{
    public async Task<OperationResult<TicketDetailDto>> Handle(GetTicketQuery request, CancellationToken cancellationToken)
    {
        var detail = await reader.GetAsync(request.Reference, cancellationToken);
        return detail is null ? OperationResult<TicketDetailDto>.Fail("ticket_not_found", 404) : OperationResult<TicketDetailDto>.Success(detail);
    }
}
