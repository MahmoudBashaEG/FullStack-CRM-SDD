using Crm.Domain.KnowledgeBase;
using Crm.Domain.Tickets;

namespace Crm.Application.Portal;

// Customer-safe read models (P4/P10). No priority, assignee, creator, escalation, SLA, staff names or user ids — by construction.

public sealed record PortalTicketSummary(Guid Id, string Subject, TicketCategory Category, TicketStatus Status, DateTimeOffset CreatedAtUtc);

public sealed record PortalFeedbackDetails(int Rating, string? Comment, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record PortalTicketDetails(
    Guid Id, string Subject, string Description, TicketCategory Category, TicketStatus Status, DateTimeOffset CreatedAtUtc,
    PortalFeedbackDetails? Feedback)
{
    public bool CanSubmitFeedback => TicketFeedback.IsEligible(Status);
}

/// <summary>Search already trimmed (subject, or exact ticket id). Status null = any.</summary>
public sealed record PortalTicketListFilter(string? Search, TicketStatus? Status);

/// <summary>Raw safe columns of one Created/StatusChanged activity row (P5). Never actor, field, agents or reason.</summary>
public sealed record PortalActivityEvent(long Id, TicketActivityType Type, DateTimeOffset OccurredAtUtc, string? OldValue, string? NewValue);

public sealed record PortalTicketHistory(DateTimeOffset TicketCreatedAtUtc, IReadOnlyList<PortalActivityEvent> Events);

public enum PortalTimelineEntryKind
{
    Created = 0,
    StatusChanged = 1,
}

/// <summary>IsRecorded false = synthesized from Tickets.CreatedAtUtc for a ticket that pre-dates activity tracking.</summary>
public sealed record PortalTimelineEntry(
    PortalTimelineEntryKind Kind, TicketStatus? FromStatus, TicketStatus Status, DateTimeOffset OccurredAtUtc, bool IsRecorded);

public sealed record HelpCenterCategory(Guid Id, string Name);

/// <summary>List row — never the body, never author/status/visibility.</summary>
public sealed record HelpCenterArticleSummary(
    Guid Id, string Title, string? Summary, KnowledgeArticleType Type, Guid CategoryId, string CategoryName, DateTimeOffset PublishedAtUtc);

public sealed record HelpCenterArticleDetails(
    Guid Id, string Title, string? Summary, string Body, KnowledgeArticleType Type, Guid CategoryId, string CategoryName,
    DateTimeOffset PublishedAtUtc, DateTimeOffset UpdatedAtUtc);

/// <summary>Search already trimmed (title, summary or body).</summary>
public sealed record HelpCenterArticleFilter(string? Search, KnowledgeArticleType? Type, Guid? CategoryId);
