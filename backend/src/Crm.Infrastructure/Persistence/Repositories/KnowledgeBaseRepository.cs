using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class KnowledgeBaseRepository(CrmDbContext dbContext) : IKnowledgeBaseRepository
{
    public void AddArticle(KnowledgeArticle article) => dbContext.KnowledgeArticles.Add(article);

    public Task<KnowledgeArticle?> GetVisibleArticleAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default) =>
        Visible(dbContext.KnowledgeArticles, actor).SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<KnowledgeArticleDetails?> FindArticleDetailsAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default)
    {
        var isSuperAdmin = actor.IsSuperAdmin;
        var agentId = actor.UserId;
        return (
            from a in Visible(dbContext.KnowledgeArticles.AsNoTracking(), actor)
            where a.Id == id
            join c in dbContext.KnowledgeCategories on a.CategoryId equals c.Id
            join author in dbContext.Users on a.AuthorUserId equals author.Id
            join updater in dbContext.Users on a.UpdatedByUserId equals updater.Id
            from publisher in dbContext.Users.Where(u => u.Id == a.PublishedByUserId).DefaultIfEmpty()
            select new KnowledgeArticleDetails(
                a.Id, a.Title, a.Summary, a.Body, a.Type, a.CategoryId, c.Name, c.IsActive, a.Visibility, a.Status,
                a.AuthorUserId, author.FirstName + " " + author.LastName, updater.FirstName + " " + updater.LastName,
                a.CreatedAtUtc, a.UpdatedAtUtc, a.SubmittedAtUtc, a.ReturnReason, a.PublishedAtUtc,
                publisher == null ? null : publisher.FirstName + " " + publisher.LastName, a.ArchivedAtUtc,
                a.Status != KnowledgeArticleStatus.Archived
                    && (isSuperAdmin || (a.AuthorUserId == agentId && a.Status == KnowledgeArticleStatus.Draft)),
                a.Status == KnowledgeArticleStatus.Draft && (isSuperAdmin || a.AuthorUserId == agentId)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<KnowledgeArticleSummary>> ListArticlesAsync(
        KnowledgeArticleListFilter filter, StaffActor actor, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Visible(dbContext.KnowledgeArticles.AsNoTracking(), actor);

        if (filter.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }

        if (filter.Type is { } type)
        {
            query = query.Where(a => a.Type == type);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(a => a.CategoryId == categoryId);
        }

        if (filter.Mine)
        {
            var me = actor.UserId;
            query = query.Where(a => a.AuthorUserId == me);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            // Parameterized LIKE; EF Core escapes wildcards for string.Contains. Body is nvarchar(max).
            var search = filter.Search;
            query = query.Where(a => a.Title.Contains(search) || (a.Summary != null && a.Summary.Contains(search)) || a.Body.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // orderby before the record projection: EF cannot order by record members. Body is never selected.
        var items = await (
            from a in query
            join c in dbContext.KnowledgeCategories on a.CategoryId equals c.Id
            join author in dbContext.Users on a.AuthorUserId equals author.Id
            orderby a.UpdatedAtUtc descending, a.Id descending
            select new KnowledgeArticleSummary(
                a.Id, a.Title, a.Summary, a.Type, a.CategoryId, c.Name, a.Visibility, a.Status, a.AuthorUserId,
                author.FirstName + " " + author.LastName, a.UpdatedAtUtc, a.PublishedAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<KnowledgeArticleSummary>(items, page, pageSize, totalCount);
    }

    public void AddCategory(KnowledgeCategory category) => dbContext.KnowledgeCategories.Add(category);

    public Task<KnowledgeCategory?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> CategoryNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeCategories.AnyAsync(c => c.Name == name && (exceptId == null || c.Id != exceptId), cancellationToken);

    public Task<KnowledgeCategoryDetails> GetCategoryDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeCategories.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new KnowledgeCategoryDetails(c.Id, c.Name, c.IsActive, c.UpdatedAtUtc))
            .SingleAsync(cancellationToken);

    public async Task<IReadOnlyList<KnowledgeCategoryDetails>> ListCategoriesAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        await dbContext.KnowledgeCategories.AsNoTracking()
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new KnowledgeCategoryDetails(c.Id, c.Name, c.IsActive, c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    // Applied first, in SQL. SuperAdmin: everything (incl. Archived). Agent: Published + own Draft/ReadyForPublication (K1/K2).
    private static IQueryable<KnowledgeArticle> Visible(IQueryable<KnowledgeArticle> articles, StaffActor actor)
    {
        if (actor.IsSuperAdmin)
        {
            return articles;
        }

        var agentId = actor.UserId;
        return articles.Where(a =>
            a.Status == KnowledgeArticleStatus.Published
            || (a.AuthorUserId == agentId
                && (a.Status == KnowledgeArticleStatus.Draft || a.Status == KnowledgeArticleStatus.ReadyForPublication)));
    }
}
