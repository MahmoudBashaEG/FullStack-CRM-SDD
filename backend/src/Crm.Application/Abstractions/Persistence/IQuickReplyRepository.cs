using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.QuickReplies;
using Crm.Domain.QuickReplies;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Visibility is applied first, in SQL: SuperAdmin ⇒ Shared only; Agent ⇒ active Shared + own Personal (archived or not).
/// </summary>
public interface IQuickReplyRepository
{
    void Add(QuickReplyTemplate template);

    /// <summary>Tracked, for updates. Null when missing or not visible to the actor.</summary>
    Task<QuickReplyTemplate?> GetVisibleAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default);

    Task<QuickReplyDetails?> FindDetailsByIdAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default);

    /// <summary>Ordered Title ASC, Id ASC.</summary>
    Task<PagedResult<QuickReplyDetails>> ListAsync(
        QuickReplyListFilter filter, StaffActor actor, int page, int pageSize, CancellationToken cancellationToken = default);
}
