using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using MediatR;
namespace Crm.Application.Tickets.Queries;

public sealed record GetTicketHistoryQuery(string Reference, int Page = 1, int PageSize = 25) : IRequest<OperationResult<TicketPageDto<TicketHistoryDto>>>;
public sealed class GetTicketHistoryHandler(ITicketReadService reader) : IRequestHandler<GetTicketHistoryQuery, OperationResult<TicketPageDto<TicketHistoryDto>>>
{
    public async Task<OperationResult<TicketPageDto<TicketHistoryDto>>> Handle(GetTicketHistoryQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100) return OperationResult<TicketPageDto<TicketHistoryDto>>.Fail("validation_failed", 400, new() { ["page"] = ["Select valid paging values."] });
        var page = await reader.GetHistoryAsync(request.Reference, request.Page, request.PageSize, cancellationToken);
        return page is null ? OperationResult<TicketPageDto<TicketHistoryDto>>.Fail("ticket_not_found", 404) : OperationResult<TicketPageDto<TicketHistoryDto>>.Success(page);
    }
}
