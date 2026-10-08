using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Domain.Tickets;
using MediatR;
using Crm.Application.Tickets.Validation;
namespace Crm.Application.Tickets.Queries;

public sealed record ListTicketsQuery(string? Q = null, string? Status = null, string? CategoryId = null, string? Priority = null, string? Assignment = null, int Page = 1, int PageSize = 25) : IRequest<OperationResult<TicketPageDto<TicketSummaryDto>>>;
public sealed record TicketListCriteria(string? Q, TicketStatus? Status, Guid? CategoryId, TicketPriority? Priority, bool Unassigned, Guid? AgentId, int Page, int PageSize);
public sealed class ListTicketsHandler(ITicketReadService reader) : IRequestHandler<ListTicketsQuery, OperationResult<TicketPageDto<TicketSummaryDto>>>
{
    public async Task<OperationResult<TicketPageDto<TicketSummaryDto>>> Handle(ListTicketsQuery query, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var validation = ListTicketsValidator.Validate(query);
        if (validation.Error is not null) return new(null, validation.Error);
        return OperationResult<TicketPageDto<TicketSummaryDto>>.Success(await reader.ListAsync(validation.Value!, token));
    }
}
