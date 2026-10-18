using Crm.Application.QuickReplies;
using Crm.Domain.QuickReplies;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.QuickReplies;

// No scope or owner: both are derived from the caller's role at creation and never change.
public sealed record CreateQuickReplyRequest(string Title, string Body, TicketCategory? Category);

public sealed record UpdateQuickReplyRequest(string Title, string Body, TicketCategory? Category);

public sealed record QuickReplyResponse(
    Guid Id, string Title, string Body, TicketCategory Category, QuickReplyScope Scope, Guid? OwnerUserId,
    string CreatedByName, string UpdatedByName, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsArchived, bool CanEdit)
{
    public static QuickReplyResponse From(QuickReplyDetails q) =>
        new(q.Id, q.Title, q.Body, q.Category, q.Scope, q.OwnerUserId, q.CreatedByName, q.UpdatedByName,
            q.CreatedAtUtc, q.UpdatedAtUtc, q.IsArchived, q.CanEdit);
}
