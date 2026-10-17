using Crm.Domain.Tickets;

namespace Crm.Application.AgentDashboard;

/// <summary>
/// Live, read-only view of the tickets currently assigned to one Agent (DASH-001). Built per request; nothing is cached or stored.
/// Counts overlap (one ticket can be Open, Escalated and High/Urgent at once) and must never be added together.
/// </summary>
public sealed record AgentDashboardSnapshot(
    AgentWorkloadSummary Summary,
    IReadOnlyList<AttentionTicket> AttentionQueue,
    int AttentionQueueLimit,
    DateTimeOffset GeneratedAtUtc);

/// <summary>Active = Open + InProgress (disjoint). Escalated and HighOrUrgent count only Open/InProgress tickets.</summary>
public sealed record AgentWorkloadSummary(int Open, int InProgress, int Active, int Escalated, int HighOrUrgent);

/// <summary>Raw SQL aggregate; Active is derived by the handler.</summary>
public sealed record AgentWorkloadCounts(int Open, int InProgress, int Escalated, int HighOrUrgent);

/// <summary>Queue row. Customer name/email joined from Customers at query time; no Identity fields.</summary>
public sealed record AttentionTicket(
    Guid Id,
    string Subject,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    bool IsEscalated,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset UpdatedAtUtc);

public static class AgentDashboardRules
{
    public const int AttentionQueueLimit = 10;
}
