using Crm.Application.Portal;
using Crm.Domain.KnowledgeBase;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Portal;

// CP2: exactly three fields. No customer, priority, status, assignee or role field exists, so forged JSON members are ignored.
public sealed record SubmitPortalTicketRequest(string Subject, string Description, TicketCategory? Category);

public sealed record SavePortalFeedbackRequest(int? Rating, string? Comment);

public sealed record PortalTicketSummaryResponse(Guid Id, string Subject, TicketCategory Category, TicketStatus Status, DateTimeOffset CreatedAtUtc)
{
    public static PortalTicketSummaryResponse From(PortalTicketSummary t) => new(t.Id, t.Subject, t.Category, t.Status, t.CreatedAtUtc);
}

public sealed record PortalFeedbackResponse(int Rating, string? Comment, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record PortalTicketResponse(
    Guid Id, string Subject, string Description, TicketCategory Category, TicketStatus Status, DateTimeOffset CreatedAtUtc,
    bool CanSubmitFeedback, PortalFeedbackResponse? Feedback)
{
    public static PortalTicketResponse From(PortalTicketDetails t) =>
        new(t.Id, t.Subject, t.Description, t.Category, t.Status, t.CreatedAtUtc, t.CanSubmitFeedback,
            t.Feedback is { } f ? new PortalFeedbackResponse(f.Rating, f.Comment, f.CreatedAtUtc, f.UpdatedAtUtc) : null);
}

public sealed record PortalTimelineEntryResponse(
    PortalTimelineEntryKind Kind, TicketStatus? FromStatus, TicketStatus Status, DateTimeOffset OccurredAtUtc, bool IsRecorded)
{
    public static PortalTimelineEntryResponse From(PortalTimelineEntry e) => new(e.Kind, e.FromStatus, e.Status, e.OccurredAtUtc, e.IsRecorded);
}

public sealed record HelpCenterCategoryResponse(Guid Id, string Name)
{
    public static HelpCenterCategoryResponse From(HelpCenterCategory c) => new(c.Id, c.Name);
}

public sealed record HelpCenterArticleSummaryResponse(
    Guid Id, string Title, string? Summary, KnowledgeArticleType Type, Guid CategoryId, string CategoryName, DateTimeOffset PublishedAtUtc)
{
    public static HelpCenterArticleSummaryResponse From(HelpCenterArticleSummary a) =>
        new(a.Id, a.Title, a.Summary, a.Type, a.CategoryId, a.CategoryName, a.PublishedAtUtc);
}

public sealed record HelpCenterArticleResponse(
    Guid Id, string Title, string? Summary, string Body, KnowledgeArticleType Type, Guid CategoryId, string CategoryName,
    DateTimeOffset PublishedAtUtc, DateTimeOffset UpdatedAtUtc)
{
    public static HelpCenterArticleResponse From(HelpCenterArticleDetails a) =>
        new(a.Id, a.Title, a.Summary, a.Body, a.Type, a.CategoryId, a.CategoryName, a.PublishedAtUtc, a.UpdatedAtUtc);
}
