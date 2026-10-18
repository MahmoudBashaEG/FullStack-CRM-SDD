using Crm.Domain.KnowledgeBase;

namespace Crm.Application.KnowledgeBase;

public sealed record KnowledgeCategoryDetails(Guid Id, string Name, bool IsActive, DateTimeOffset UpdatedAtUtc);

/// <summary>List row — never contains the body.</summary>
public sealed record KnowledgeArticleSummary(
    Guid Id, string Title, string? Summary, KnowledgeArticleType Type, Guid CategoryId, string CategoryName,
    KnowledgeArticleVisibility Visibility, KnowledgeArticleStatus Status, Guid AuthorUserId, string AuthorName,
    DateTimeOffset UpdatedAtUtc, DateTimeOffset? PublishedAtUtc);

/// <summary>CanEdit / CanSubmit are computed for the requesting actor. Display only — commands re-check.</summary>
public sealed record KnowledgeArticleDetails(
    Guid Id, string Title, string? Summary, string Body, KnowledgeArticleType Type, Guid CategoryId, string CategoryName,
    bool CategoryIsActive, KnowledgeArticleVisibility Visibility, KnowledgeArticleStatus Status, Guid AuthorUserId, string AuthorName,
    string UpdatedByName, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? SubmittedAtUtc, string? ReturnReason,
    DateTimeOffset? PublishedAtUtc, string? PublishedByName, DateTimeOffset? ArchivedAtUtc, bool CanEdit, bool CanSubmit);

/// <summary>Search already trimmed (title, summary or body). Mine = only the caller's own articles.</summary>
public sealed record KnowledgeArticleListFilter(
    string? Search, KnowledgeArticleType? Type, Guid? CategoryId, KnowledgeArticleStatus? Status, bool Mine);

/// <summary>Metadata only (I6): never the body, so archived content is not exposed through a link.</summary>
public sealed record TicketKnowledgeLinkDetails(
    long Id, Guid TicketId, Guid ArticleId, string ArticleTitle, KnowledgeArticleType ArticleType, string CategoryName,
    KnowledgeArticleStatus ArticleStatus, string LinkedByName, DateTimeOffset LinkedAtUtc);
