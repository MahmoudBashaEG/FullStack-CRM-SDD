using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Portal;
using Crm.Domain.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

/// <summary>PORTAL-001 CP4: Published AND Public in SQL for every query. No author/publisher/status/visibility columns are selected.</summary>
internal sealed class HelpCenterRepository(CrmDbContext dbContext) : IHelpCenterRepository
{
    public async Task<PagedResult<HelpCenterArticleSummary>> ListArticlesAsync(
        HelpCenterArticleFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = PublicArticles();

        if (filter.Type is { } type)
        {
            query = query.Where(a => a.Type == type);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(a => a.CategoryId == categoryId);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            // Parameterized LIKE, as KnowledgeBaseRepository.ListArticlesAsync.
            var search = filter.Search;
            query = query.Where(a => a.Title.Contains(search) || (a.Summary != null && a.Summary.Contains(search)) || a.Body.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // orderby before the record projection: EF cannot order by record members. Body is never selected.
        var items = await (
                from a in query
                join c in dbContext.KnowledgeCategories on a.CategoryId equals c.Id
                orderby a.PublishedAtUtc descending, a.Id descending
                select new HelpCenterArticleSummary(
                    a.Id, a.Title, a.Summary, a.Type, a.CategoryId, c.Name, a.PublishedAtUtc ?? a.UpdatedAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<HelpCenterArticleSummary>(items, page, pageSize, totalCount);
    }

    public Task<HelpCenterArticleDetails?> FindArticleAsync(Guid id, CancellationToken cancellationToken = default) =>
        (from a in PublicArticles()
         where a.Id == id
         join c in dbContext.KnowledgeCategories on a.CategoryId equals c.Id
         select new HelpCenterArticleDetails(
             a.Id, a.Title, a.Summary, a.Body, a.Type, a.CategoryId, c.Name, a.PublishedAtUtc ?? a.UpdatedAtUtc, a.UpdatedAtUtc))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<HelpCenterCategory>> ListCategoriesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.KnowledgeCategories.AsNoTracking()
            .Where(c => PublicArticles().Any(a => a.CategoryId == c.Id))
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new HelpCenterCategory(c.Id, c.Name))
            .ToListAsync(cancellationToken);

    // The single customer visibility rule (CP4), applied first in every statement.
    private IQueryable<KnowledgeArticle> PublicArticles() =>
        dbContext.KnowledgeArticles.AsNoTracking()
            .Where(a => a.Status == KnowledgeArticleStatus.Published && a.Visibility == KnowledgeArticleVisibility.Public);
}
