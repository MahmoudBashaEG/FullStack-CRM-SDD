using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using FluentValidation;
using Mediator;

namespace Crm.Application.SlaAlerts.ListSlaAlerts;

public sealed record ListSlaAlertsQuery(
    StaffActor Actor, bool UnreadOnly = false, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<SlaAlertDetails>>;

public sealed class ListSlaAlertsQueryHandler(ISlaAlertRepository alerts)
    : IQueryHandler<ListSlaAlertsQuery, PagedResult<SlaAlertDetails>>
{
    public async ValueTask<PagedResult<SlaAlertDetails>> Handle(ListSlaAlertsQuery query, CancellationToken cancellationToken) =>
        await alerts.ListAsync(query.Actor, query.UnreadOnly, query.Page, Math.Min(query.PageSize, Paging.MaxPageSize), cancellationToken);
}

public sealed class ListSlaAlertsValidator : AbstractValidator<ListSlaAlertsQuery>
{
    public ListSlaAlertsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
    }
}
