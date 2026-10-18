using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.Administration;
using FluentValidation;
using Mediator;

namespace Crm.Application.Administration.ListAuditLogs;

public sealed record ListAuditLogsQuery(
    AdminAuditAction? Action, DateOnly? From, DateOnly? To, string? Search, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<AdminAuditEntryDetails>>;

public sealed class ListAuditLogsQueryHandler(IAdminAuditRepository audit)
    : IQueryHandler<ListAuditLogsQuery, PagedResult<AdminAuditEntryDetails>>
{
    public async ValueTask<PagedResult<AdminAuditEntryDetails>> Handle(ListAuditLogsQuery query, CancellationToken cancellationToken)
    {
        // UTC calendar days, inclusive: [from 00:00Z, to+1 00:00Z).
        DateTimeOffset? fromUtc = query.From is { } f ? new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
        DateTimeOffset? toExclusive = query.To is { } t ? new DateTimeOffset(t.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize);
        return await audit.ListAsync(query.Action, fromUtc, toExclusive, search, query.Page, pageSize, cancellationToken);
    }
}

public sealed class ListAuditLogsValidator : AbstractValidator<ListAuditLogsQuery>
{
    public ListAuditLogsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Action).IsInEnum();
        RuleFor(q => q.Search).MaximumLength(AdministrationRules.SearchMaxLength);
        RuleFor(q => q.From).GreaterThanOrEqualTo(AdministrationRules.EarliestAuditDate).When(q => q.From is not null);
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From!.Value).WithMessage("'To' must be on or after 'From'.")
            .When(q => q.From is not null && q.To is not null);
    }
}
