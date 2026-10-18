using Crm.Application.AgentTasks;
using Crm.Domain.AgentTasks;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.AgentTasks;

/// <summary>
/// Version is the base64 rowversion (the same value as the ETag header, without quotes). linkedTicketId/Subject are null for an Agent
/// when the link is Inaccessible. No customer data.
/// </summary>
public sealed record AgentTaskResponse(
    Guid Id,
    string Title,
    string? Description,
    AgentTaskStatus Status,
    Guid AssignedAgentUserId,
    string AssignedAgentName,
    Guid CreatedByUserId,
    string CreatedByName,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? RemindAtUtc,
    DateTimeOffset? ReminderSentAtUtc,
    LinkedTicketState LinkedTicketState,
    Guid? LinkedTicketId,
    string? LinkedTicketSubject,
    bool IsOverdue,
    bool IsDueSoon,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool IsArchived,
    DateTimeOffset? ArchivedAtUtc,
    string Version)
{
    public static AgentTaskResponse From(AgentTaskDetails t) =>
        new(t.Id, t.Title, t.Description, t.Status, t.AssignedAgentUserId, t.AssignedAgentName, t.CreatedByUserId, t.CreatedByName,
            t.DueAtUtc, t.RemindAtUtc, t.ReminderSentAtUtc, t.LinkedTicketState, t.LinkedTicketId, t.LinkedTicketSubject,
            t.IsOverdue, t.IsDueSoon, t.CreatedAtUtc, t.UpdatedAtUtc, t.CompletedAtUtc, t.IsArchived, t.ArchivedAtUtc,
            Convert.ToBase64String(t.Version));
}

public sealed record AgentTaskSummaryResponse(
    int Pending, int Overdue, int DueSoon, IReadOnlyList<AgentTaskResponse> NextTasks, int PreviewLimit, DateTimeOffset GeneratedAtUtc)
{
    public static AgentTaskSummaryResponse From(AgentTaskSummary s) =>
        new(s.Counts.Pending, s.Counts.Overdue, s.Counts.DueSoon, s.NextTasks.Select(AgentTaskResponse.From).ToList(),
            s.PreviewLimit, s.GeneratedAtUtc);
}

public sealed record LinkableTicketResponse(Guid Id, string Subject, TicketStatus Status, TicketPriority Priority)
{
    public static LinkableTicketResponse From(LinkableTicket t) => new(t.Id, t.Subject, t.Status, t.Priority);
}
