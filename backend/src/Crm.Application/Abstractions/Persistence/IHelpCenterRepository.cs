using Crm.Application.Common.Paging;
using Crm.Application.Portal;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// PORTAL-001 CP4: Status == Published AND Visibility == Public in SQL for every query (list, detail, categories).
/// Separate from IKnowledgeBaseRepository (staff visibility includes Internal), which is unchanged. Read-only.
/// </summary>
public interface IHelpCenterRepository
{
    /// <summary>Ordered PublishedAtUtc DESC, Id DESC. Rows never contain the body.</summary>
    Task<PagedResult<HelpCenterArticleSummary>> ListArticlesAsync(
        HelpCenterArticleFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Null when missing, not Published or not Public.</summary>
    Task<HelpCenterArticleDetails?> FindArticleAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Only categories holding at least one Published + Public article. Ordered Name, Id.</summary>
    Task<IReadOnlyList<HelpCenterCategory>> ListCategoriesAsync(CancellationToken cancellationToken = default);
}
