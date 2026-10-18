using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Ticket ↔ article links (K4). Ticket access uses the existing ticket boundary (TicketRepository.Scope):
/// agentScope null = SuperAdmin (any ticket), otherwise only tickets currently assigned to that Agent. Collaboration membership is ignored.
/// The ticket row is never tracked, so links can never change it.
/// </summary>
public interface ITicketKnowledgeLinkRepository
{
    Task<bool> TicketIsVisibleAsync(Guid ticketId, Guid? agentScope, CancellationToken cancellationToken = default);

    Task<bool> LinkExistsAsync(Guid ticketId, Guid articleId, CancellationToken cancellationToken = default);

    Task<int> CountForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    void Add(TicketKnowledgeArticleLink link);

    /// <summary>Tracked. Null when not linked.</summary>
    Task<TicketKnowledgeArticleLink?> GetAsync(Guid ticketId, Guid articleId, CancellationToken cancellationToken = default);

    void Remove(TicketKnowledgeArticleLink link);

    Task<TicketKnowledgeLinkDetails> GetDetailsAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Ordered LinkedAtUtc DESC, Id DESC. Includes links to archived/unpublished articles (status shown, body never).</summary>
    Task<IReadOnlyList<TicketKnowledgeLinkDetails>> ListForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
