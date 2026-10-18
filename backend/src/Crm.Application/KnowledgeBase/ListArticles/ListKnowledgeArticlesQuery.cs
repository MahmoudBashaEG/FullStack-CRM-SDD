using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.KnowledgeBase;
using FluentValidation;
using Mediator;

namespace Crm.Application.KnowledgeBase.ListArticles;

public sealed record ListKnowledgeArticlesQuery(
    StaffActor Actor,
    string? Search,
    KnowledgeArticleType? Type,
    Guid? CategoryId,
    KnowledgeArticleStatus? Status,
    bool Mine = false,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<KnowledgeArticleSummary>>;

public sealed class ListKnowledgeArticlesQueryHandler(IKnowledgeBaseRepository knowledgeBase)
    : IQueryHandler<ListKnowledgeArticlesQuery, PagedResult<KnowledgeArticleSummary>>
{
    public async ValueTask<PagedResult<KnowledgeArticleSummary>> Handle(ListKnowledgeArticlesQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var filter = new KnowledgeArticleListFilter(search, query.Type, query.CategoryId, query.Status, query.Mine);
        return await knowledgeBase.ListArticlesAsync(
            filter, query.Actor, query.Page, Math.Min(query.PageSize, Paging.MaxPageSize), cancellationToken);
    }
}

public sealed class ListKnowledgeArticlesValidator : AbstractValidator<ListKnowledgeArticlesQuery>
{
    public ListKnowledgeArticlesValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(KnowledgeBaseRules.SearchMaxLength);
        RuleFor(q => q.Type).IsInEnum();
        RuleFor(q => q.Status).IsInEnum();
    }
}
