namespace Crm.Api.Contracts.AgentTasks;

// assignedAgentUserId: SuperAdmin only (required there, an active Agent); Agents omit it (the task is theirs).
public sealed record CreateAgentTaskRequest(
    string Title, string? Description, Guid? AssignedAgentUserId, Guid? TicketId, DateTimeOffset? DueAtUtc, DateTimeOffset? RemindAtUtc);

// Title, description and due time only. Link, owner and reminder have their own endpoints.
public sealed record UpdateAgentTaskRequest(string Title, string? Description, DateTimeOffset? DueAtUtc);

public sealed record SetTaskTicketLinkRequest(Guid TicketId);

// Non-nullable: a missing value fails model validation (400). Cancelling is DELETE.
public sealed record ScheduleTaskReminderRequest(DateTimeOffset RemindAtUtc);

public sealed record ReassignAgentTaskRequest(Guid? AssignedAgentUserId, bool RemoveTicketLink);
