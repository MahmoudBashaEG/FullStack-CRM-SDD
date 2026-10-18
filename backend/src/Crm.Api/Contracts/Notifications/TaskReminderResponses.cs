using Crm.Application.AgentTasks;
using Crm.Application.Common.Validation;
using Crm.Domain.AgentTasks;

namespace Crm.Api.Contracts.Notifications;

/// <summary>
/// WasMissed = issued more than 5 minutes after its scheduled time: delayed issuance only (no check reached the server in time).
/// It does not prove the Agent was offline. No recipient / transferred-from user ids are exposed.
/// </summary>
public sealed record TaskReminderResponse(
    long Id,
    Guid TaskId,
    string TaskTitle,
    AgentTaskStatus TaskStatus,
    bool TaskIsArchived,
    DateTimeOffset ScheduledForUtc,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? ReadAtUtc,
    bool IsRead,
    bool WasMissed,
    DateTimeOffset? TransferredAtUtc)
{
    public static TaskReminderResponse From(TaskReminderDetails r) =>
        new(r.Id, r.TaskId, r.TaskTitle, r.TaskStatus, r.TaskIsArchived, r.ScheduledForUtc, r.IssuedAtUtc, r.ReadAtUtc,
            r.ReadAtUtc is not null, r.IssuedAtUtc > r.ScheduledForUtc + TaskReminderRules.MissedThreshold, r.TransferredAtUtc);
}

public sealed record TaskReminderCheckResponse(int UnreadCount, int IssuedNow, DateTimeOffset CheckedAtUtc)
{
    public static TaskReminderCheckResponse From(TaskReminderCheck c) => new(c.UnreadCount, c.IssuedNow, c.CheckedAtUtc);
}
