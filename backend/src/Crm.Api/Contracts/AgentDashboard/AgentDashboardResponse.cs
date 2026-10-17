using Crm.Application.AgentDashboard;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.AgentDashboard;

/// <summary>Counts overlap and are not additive; Active = Open + InProgress. AttentionQueue is the top AttentionQueueLimit only.</summary>
public sealed record AgentDashboardResponse(
    AgentWorkloadSummaryResponse Summary,
    IReadOnlyList<AttentionTicketResponse> AttentionQueue,
    int AttentionQueueLimit,
    DateTimeOffset GeneratedAtUtc)
{
    public static AgentDashboardResponse From(AgentDashboardSnapshot s) =>
        new(new AgentWorkloadSummaryResponse(s.Summary.Open, s.Summary.InProgress, s.Summary.Active, s.Summary.Escalated, s.Summary.HighOrUrgent),
            s.AttentionQueue.Select(AttentionTicketResponse.From).ToList(),
            s.AttentionQueueLimit,
            s.GeneratedAtUtc);
}

public sealed record AgentWorkloadSummaryResponse(int Open, int InProgress, int Active, int Escalated, int HighOrUrgent);

/// <summary>Customer shown by id + name + email only (no Identity fields).</summary>
public sealed record AttentionTicketResponse(
    Guid Id,
    string Subject,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    bool IsEscalated,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset UpdatedAtUtc)
{
    public static AttentionTicketResponse From(AttentionTicket t) =>
        new(t.Id, t.Subject, t.Category, t.Priority, t.Status, t.IsEscalated, t.CustomerId, t.CustomerName, t.CustomerEmail, t.UpdatedAtUtc);
}
