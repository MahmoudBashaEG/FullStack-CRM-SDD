namespace Crm.Domain.AgentTasks;

/// <summary>
/// One issued in-app reminder (DASH-002 D4). Inserted only by the repository's idempotent INSERT…SELECT; unique (TaskId, ReminderGeneration).
/// RecipientUserId = the Agent who currently holds it: set at issuance; when the task is reassigned, UNREAD rows move to the new owner
/// (TransferredFromUserId / TransferredAtUtc record the last move); READ rows never move — they are the reader's history.
/// Changed only by conditional set-based statements in TaskReminderRepository (no load-then-save), so it has no methods.
/// </summary>
public sealed class TaskReminderNotification
{
    private TaskReminderNotification() { } // EF Core

    public long Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public int ReminderGeneration { get; private set; }
    public DateTimeOffset ScheduledForUtc { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }
    public Guid? TransferredFromUserId { get; private set; }
    public DateTimeOffset? TransferredAtUtc { get; private set; }
}
