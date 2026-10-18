namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// Ticket ↔ article association (K4). Insert/delete only; never changes the ticket (status, assignment, SLA, UpdatedAtUtc, activity).
/// Kept when the article is archived (traceability). Id is a store-generated identity.
/// </summary>
public sealed class TicketKnowledgeArticleLink
{
    private TicketKnowledgeArticleLink() { } // EF Core

    public long Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid ArticleId { get; private set; }
    public Guid LinkedByUserId { get; private set; }
    public DateTimeOffset LinkedAtUtc { get; private set; }

    public static TicketKnowledgeArticleLink Create(Guid ticketId, Guid articleId, Guid linkedByUserId, DateTimeOffset now) =>
        new() { TicketId = ticketId, ArticleId = articleId, LinkedByUserId = linkedByUserId, LinkedAtUtc = now };
}
