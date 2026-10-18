using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.KnowledgeBase;
using FluentValidation;
using Mediator;

namespace Crm.Application.Portal.ListHelpCenterArticles;

public sealed record ListHelpCenterArticlesQuery(
    Guid UserId, string? Search, KnowledgeArticleType? Type, Guid? CategoryId, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<HelpCenterArticleSummary>>;

public sealed class ListHelpCenterArticlesQueryHandler(ICustomerRepository customers, IHelpCenterRepository helpCenter)
    : IQueryHandler<ListHelpCenterArticlesQuery, PagedResult<HelpCenterArticleSummary>>
{
    public async ValueTask<PagedResult<HelpCenterArticleSummary>> Handle(ListHelpCenterArticlesQuery query, CancellationToken cancellationToken)
    {
        await customers.ResolveIdAsync(query.UserId, cancellationToken); // CP1: linked Customers only; the id itself is unused
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize);
        return await helpCenter.ListArticlesAsync(new HelpCenterArticleFilter(search, query.Type, query.CategoryId), query.Page, pageSize, cancellationToken);
    }
}

public sealed class ListHelpCenterArticlesValidator : AbstractValidator<ListHelpCenterArticlesQuery>
{
    public ListHelpCenterArticlesValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(KnowledgeBaseRules.SearchMaxLength);
        RuleFor(q => q.Type).IsInEnum();
    }
}
