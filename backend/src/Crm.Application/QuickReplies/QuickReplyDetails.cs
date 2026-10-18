using Crm.Domain.QuickReplies;
using Crm.Domain.Tickets;

namespace Crm.Application.QuickReplies;

/// <summary>CanEdit is computed for the requesting actor (Agent: own Personal; SuperAdmin: Shared). Display only — commands re-check.</summary>
public sealed record QuickReplyDetails(
    Guid Id, string Title, string Body, TicketCategory Category, QuickReplyScope Scope, Guid? OwnerUserId,
    string CreatedByName, string UpdatedByName, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsArchived, bool CanEdit);

/// <summary>Search already trimmed (title or body). Archived: false = active only (default).</summary>
public sealed record QuickReplyListFilter(string? Search, TicketCategory? Category, QuickReplyScope? Scope, bool Archived);
