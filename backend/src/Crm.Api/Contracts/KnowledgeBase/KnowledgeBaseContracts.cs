using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;

namespace Crm.Api.Contracts.KnowledgeBase;

// No status, author, publisher or role fields: they come from the workflow endpoints and the caller's token only (K1).
public sealed record SaveKnowledgeArticleRequest(
    string Title, string? Summary, string Body, KnowledgeArticleType? Type, Guid? CategoryId, KnowledgeArticleVisibility? Visibility);

public sealed record ReturnKnowledgeArticleRequest(string? Reason);

public sealed record CreateKnowledgeCategoryRequest(string Name);

public sealed record UpdateKnowledgeCategoryRequest(string Name, bool IsActive);

public sealed record LinkKnowledgeArticleRequest(Guid ArticleId);

public sealed record KnowledgeCategoryResponse(Guid Id, string Name, bool IsActive, DateTimeOffset UpdatedAtUtc)
{
    public static KnowledgeCategoryResponse From(KnowledgeCategoryDetails c) => new(c.Id, c.Name, c.IsActive, c.UpdatedAtUtc);
}

public sealed record KnowledgeArticleSummaryResponse(
    Guid Id, string Title, string? Summary, KnowledgeArticleType Type, Guid CategoryId, string CategoryName,
    KnowledgeArticleVisibility Visibility, KnowledgeArticleStatus Status, Guid AuthorUserId, string AuthorName,
    DateTimeOffset UpdatedAtUtc, DateTimeOffset? PublishedAtUtc)
{
    public static KnowledgeArticleSummaryResponse From(KnowledgeArticleSummary a) =>
        new(a.Id, a.Title, a.Summary, a.Type, a.CategoryId, a.CategoryName, a.Visibility, a.Status, a.AuthorUserId, a.AuthorName,
            a.UpdatedAtUtc, a.PublishedAtUtc);
}

public sealed record KnowledgeArticleResponse(
    Guid Id, string Title, string? Summary, string Body, KnowledgeArticleType Type, Guid CategoryId, string CategoryName,
    bool CategoryIsActive, KnowledgeArticleVisibility Visibility, KnowledgeArticleStatus Status, Guid AuthorUserId, string AuthorName,
    string UpdatedByName, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? SubmittedAtUtc, string? ReturnReason,
    DateTimeOffset? PublishedAtUtc, string? PublishedByName, DateTimeOffset? ArchivedAtUtc, bool CanEdit, bool CanSubmit)
{
    public static KnowledgeArticleResponse From(KnowledgeArticleDetails a) =>
        new(a.Id, a.Title, a.Summary, a.Body, a.Type, a.CategoryId, a.CategoryName, a.CategoryIsActive, a.Visibility, a.Status,
            a.AuthorUserId, a.AuthorName, a.UpdatedByName, a.CreatedAtUtc, a.UpdatedAtUtc, a.SubmittedAtUtc, a.ReturnReason,
            a.PublishedAtUtc, a.PublishedByName, a.ArchivedAtUtc, a.CanEdit, a.CanSubmit);
}

public sealed record TicketKnowledgeLinkResponse(
    long Id, Guid TicketId, Guid ArticleId, string ArticleTitle, KnowledgeArticleType ArticleType, string CategoryName,
    KnowledgeArticleStatus ArticleStatus, string LinkedByName, DateTimeOffset LinkedAtUtc)
{
    public static TicketKnowledgeLinkResponse From(TicketKnowledgeLinkDetails l) =>
        new(l.Id, l.TicketId, l.ArticleId, l.ArticleTitle, l.ArticleType, l.CategoryName, l.ArticleStatus, l.LinkedByName, l.LinkedAtUtc);
}
