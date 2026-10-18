using Crm.Domain.Common;

namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// One Knowledge Base article (KB-001). AuthorUserId is fixed at creation (server-side caller). Status changes only through the
/// workflow methods below (KnowledgeArticleWorkflow); UpdateContent never touches Status, so no edit payload can publish (K1).
/// Visibility is stored independently of Status (K2). Body is Markdown source; HTML is presentation only (K3). Never hard-deleted.
/// </summary>
public sealed class KnowledgeArticle
{
    private KnowledgeArticle() { } // EF Core

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Summary { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public KnowledgeArticleType Type { get; private set; }
    public Guid CategoryId { get; private set; }
    public KnowledgeArticleVisibility Visibility { get; private set; }
    public KnowledgeArticleStatus Status { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public string? ReturnReason { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public static KnowledgeArticle CreateDraft(
        Guid id, Guid authorUserId, string title, string? summary, string body, KnowledgeArticleType type, Guid categoryId,
        KnowledgeArticleVisibility visibility, DateTimeOffset now) =>
        new()
        {
            Id = id, Title = title, Summary = summary, Body = body, Type = type, CategoryId = categoryId, Visibility = visibility,
            Status = KnowledgeArticleStatus.Draft, AuthorUserId = authorUserId, UpdatedByUserId = authorUserId,
            CreatedAtUtc = now, UpdatedAtUtc = now,
        };

    /// <summary>Same article id on every edit. Returns false when nothing changed (no save, no timestamp change).</summary>
    public bool UpdateContent(
        string title, string? summary, string body, KnowledgeArticleType type, Guid categoryId, KnowledgeArticleVisibility visibility,
        Guid actorUserId, DateTimeOffset now)
    {
        if (Status == KnowledgeArticleStatus.Archived)
        {
            throw new DomainException("Restore the article before editing it.");
        }

        if (string.Equals(Title, title, StringComparison.Ordinal) && string.Equals(Summary, summary, StringComparison.Ordinal)
            && string.Equals(Body, body, StringComparison.Ordinal) && Type == type && CategoryId == categoryId && Visibility == visibility)
        {
            return false;
        }

        Title = title;
        Summary = summary;
        Body = body;
        Type = type;
        CategoryId = categoryId;
        Visibility = visibility;
        Touch(actorUserId, now);
        return true;
    }

    public void Submit(Guid actorUserId, DateTimeOffset now)
    {
        MoveTo(KnowledgeArticleStatus.ReadyForPublication, actorUserId, now);
        SubmittedAtUtc = now;
        ReturnReason = null;
    }

    public void ReturnToDraft(string? reason, Guid superAdminUserId, DateTimeOffset now)
    {
        MoveTo(KnowledgeArticleStatus.Draft, superAdminUserId, now);
        ReturnReason = reason;
    }

    public void Publish(Guid superAdminUserId, DateTimeOffset now)
    {
        MoveTo(KnowledgeArticleStatus.Published, superAdminUserId, now);
        PublishedAtUtc = now;
        PublishedByUserId = superAdminUserId;
    }

    public void Archive(Guid superAdminUserId, DateTimeOffset now)
    {
        MoveTo(KnowledgeArticleStatus.Archived, superAdminUserId, now);
        ArchivedAtUtc = now;
    }

    /// <summary>Archived → Draft. Publication metadata is cleared; republishing records the new publisher/time. Ticket links keep the same article id.</summary>
    public void Restore(Guid superAdminUserId, DateTimeOffset now)
    {
        MoveTo(KnowledgeArticleStatus.Draft, superAdminUserId, now);
        ArchivedAtUtc = null;
        PublishedAtUtc = null;
        PublishedByUserId = null;
        SubmittedAtUtc = null;
        ReturnReason = null;
    }

    // Backstop only: handlers check the matrix first and return 409 with a friendly message.
    private void MoveTo(KnowledgeArticleStatus to, Guid actorUserId, DateTimeOffset now)
    {
        if (!KnowledgeArticleWorkflow.IsAllowed(Status, to))
        {
            throw new DomainException($"A {Status} article cannot move to {to}.");
        }

        Status = to;
        Touch(actorUserId, now);
    }

    private void Touch(Guid actorUserId, DateTimeOffset now)
    {
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
    }
}
