using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.Portal.ListTickets;

public sealed record ListPortalTicketsQuery(
    Guid UserId, string? Search, TicketStatus? Status, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<PortalTicketSummary>>;

public sealed class ListPortalTicketsQueryHandler(ICustomerRepository customers, IPortalTicketRepository portal)
    : IQueryHandler<ListPortalTicketsQuery, PagedResult<PortalTicketSummary>>
{
    public async ValueTask<PagedResult<PortalTicketSummary>> Handle(ListPortalTicketsQuery query, CancellationToken cancellationToken)
    {
        var customerId = await customers.ResolveIdAsync(query.UserId, cancellationToken);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize); // documented cap: larger values are clamped
        return await portal.ListAsync(customerId, new PortalTicketListFilter(search, query.Status), query.Page, pageSize, cancellationToken);
    }
}

public sealed class ListPortalTicketsValidator : AbstractValidator<ListPortalTicketsQuery>
{
    public ListPortalTicketsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(TicketRules.SearchMaxLength);
        RuleFor(q => q.Status).IsInEnum(); // null passes
    }
}
