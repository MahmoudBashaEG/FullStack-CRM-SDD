using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Articles and categories. Article visibility is applied first, in SQL: SuperAdmin ⇒ every article;
/// Agent ⇒ Published + own Draft/ReadyForPublication. Not visible ⇒ null (handlers return 404).
/// </summary>
public interface IKnowledgeBaseRepository
{
    void AddArticle(KnowledgeArticle article);

    /// <summary>Tracked, for updates. Null when missing or not visible to the actor.</summary>
    Task<KnowledgeArticle?> GetVisibleArticleAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleDetails?> FindArticleDetailsAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default);

    /// <summary>Ordered UpdatedAtUtc DESC, Id DESC. Rows never contain the body.</summary>
    Task<PagedResult<KnowledgeArticleSummary>> ListArticlesAsync(
        KnowledgeArticleListFilter filter, StaffActor actor, int page, int pageSize, CancellationToken cancellationToken = default);

    void AddCategory(KnowledgeCategory category);

    /// <summary>Tracked.</summary>
    Task<KnowledgeCategory?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Uses the column collation (case-insensitive by default).</summary>
    Task<bool> CategoryNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default);

    Task<KnowledgeCategoryDetails> GetCategoryDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Ordered Name, Id.</summary>
    Task<IReadOnlyList<KnowledgeCategoryDetails>> ListCategoriesAsync(bool includeInactive, CancellationToken cancellationToken = default);
}
