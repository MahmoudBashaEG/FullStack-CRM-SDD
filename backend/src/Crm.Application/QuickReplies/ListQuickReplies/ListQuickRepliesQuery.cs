using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.QuickReplies;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.QuickReplies.ListQuickReplies;

public sealed record ListQuickRepliesQuery(
    StaffActor Actor,
    string? Search,
    TicketCategory? Category,
    QuickReplyScope? Scope,
    bool Archived = false,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<QuickReplyDetails>>;

public sealed class ListQuickRepliesQueryHandler(IQuickReplyRepository templates)
    : IQueryHandler<ListQuickRepliesQuery, PagedResult<QuickReplyDetails>>
{
    public async ValueTask<PagedResult<QuickReplyDetails>> Handle(ListQuickRepliesQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var filter = new QuickReplyListFilter(search, query.Category, query.Scope, query.Archived);
        return await templates.ListAsync(filter, query.Actor, query.Page, Math.Min(query.PageSize, Paging.MaxPageSize), cancellationToken);
    }
}

public sealed class ListQuickRepliesValidator : AbstractValidator<ListQuickRepliesQuery>
{
    public ListQuickRepliesValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(QuickReplyRules.SearchMaxLength);
        RuleFor(q => q.Category).IsInEnum();
        RuleFor(q => q.Scope).IsInEnum();
    }
}
