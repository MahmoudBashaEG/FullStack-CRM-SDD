using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>
/// Read model returned by every ticket use case. Names are joined at query time from Customers / AspNetUsers
/// (no copies stored). Exposes no Identity secrets.
/// </summary>
public sealed record TicketDetails(
    Guid Id,
    string Subject,
    string Description,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    Guid CreatedByUserId,
    string CreatedByName,
    Guid? AssignedAgentUserId,
    string? AssignedAgentName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Optional list filters; Search is already trimmed (null/empty = no filter).</summary>
public sealed record TicketListFilter(
    string? Search,
    Guid? CustomerId,
    TicketStatus? Status,
    TicketCategory? Category,
    TicketPriority? Priority);
