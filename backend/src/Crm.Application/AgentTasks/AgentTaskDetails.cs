using Crm.Domain.AgentTasks;
using Crm.Domain.Tickets;

namespace Crm.Application.AgentTasks;

public enum LinkedTicketState { None, Accessible, Inaccessible }

/// <summary>Due-state filter for task lists (Pending, unarchived semantics as in docs).</summary>
public enum TaskDueFilter { Overdue, DueSoon, NoDueDate }

/// <summary>
/// Read model. Names joined at query time. LinkedTicketId/Subject are null for an Agent when the link is Inaccessible
/// (the owner is no longer the ticket's assignee); SuperAdmin always receives them. No customer data is ever included.
/// ReminderSentAtUtc = issuance time of the CURRENT generation, null while pending or when no reminder exists.
/// IsOverdue / IsDueSoon are computed in SQL against server UTC.
/// </summary>
public sealed record AgentTaskDetails(
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
    byte[] Version);

/// <summary>Search already trimmed. Archived: false (default) = unarchived only, true = archived only.
/// AssignedAgentUserId only narrows (an Agent's scope is applied first).</summary>
public sealed record AgentTaskListFilter(
    string? Search, AgentTaskStatus? Status, TaskDueFilter? DueState, bool Archived, bool? HasReminder, Guid? AssignedAgentUserId);

/// <summary>Pending, unarchived tasks owned by one Agent. Overdue and DueSoon are subsets of Pending (disjoint from each other).</summary>
public sealed record AgentTaskCounts(int Pending, int Overdue, int DueSoon);

public sealed record AgentTaskSummary(
    AgentTaskCounts Counts, IReadOnlyList<AgentTaskDetails> NextTasks, int PreviewLimit, DateTimeOffset GeneratedAtUtc);

public sealed record TaskReminderDetails(
    long Id, Guid TaskId, string TaskTitle, AgentTaskStatus TaskStatus, bool TaskIsArchived,
    DateTimeOffset ScheduledForUtc, DateTimeOffset IssuedAtUtc, DateTimeOffset? ReadAtUtc, DateTimeOffset? TransferredAtUtc);

public enum MarkReadOutcome { NotFound, MarkedRead, AlreadyRead }

/// <summary>Picker row (F5): a ticket currently assigned to the prospective task owner. No customer data.</summary>
public sealed record LinkableTicket(Guid Id, string Subject, TicketStatus Status, TicketPriority Priority);

public sealed record TaskReminderCheck(int UnreadCount, int IssuedNow, DateTimeOffset CheckedAtUtc);
