using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using FluentValidation;
using Mediator;

namespace Crm.Application.Collaboration.ListTicketMessages;

public sealed record ListTicketMessagesQuery(StaffActor Actor, Guid TicketId, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<TicketMessageDetails>>;

public sealed class ListTicketMessagesQueryHandler(ICollaborationRepository collaboration)
    : IQueryHandler<ListTicketMessagesQuery, PagedResult<TicketMessageDetails>>
{
    public async ValueTask<PagedResult<TicketMessageDetails>> Handle(ListTicketMessagesQuery query, CancellationToken cancellationToken) =>
        await collaboration.ListMessagesAsync(
            query.TicketId, query.Actor, query.Page, Math.Min(query.PageSize, Paging.MaxPageSize), cancellationToken)
        ?? throw new NotFoundException($"Ticket '{query.TicketId}' was not found.");
}

public sealed class ListTicketMessagesValidator : AbstractValidator<ListTicketMessagesQuery>
{
    public ListTicketMessagesValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
    }
}
