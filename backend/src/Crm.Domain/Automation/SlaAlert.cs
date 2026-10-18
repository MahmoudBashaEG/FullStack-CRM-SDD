using Crm.Domain.Sla;

namespace Crm.Domain.Automation;

/// <summary>
/// Persistent in-app SLA alert for one recipient (SLA-001 S7). Method-less like TaskReminderNotification: the read state changes
/// only through a conditional set-based update. Unique (ExecutionId, RecipientUserId).
/// </summary>
public sealed class SlaAlert
{
    private SlaAlert() { } // EF Core

    public long Id { get; private set; }
    public long ExecutionId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public SlaAlertType Type { get; private set; }
    public SlaKind Kind { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }

    // Store-generated ExecutionId is fixed up by EF through this navigation when both rows are saved together.
    private AutomationRuleExecution? Execution { get; set; }

    public static SlaAlert Create(
        AutomationRuleExecution execution, Guid recipientUserId, SlaAlertType type, SlaKind kind, DateTimeOffset dueAtUtc, DateTimeOffset now) =>
        new()
        {
            Execution = execution,
            TicketId = execution.TicketId,
            RecipientUserId = recipientUserId,
            Type = type,
            Kind = kind,
            DueAtUtc = dueAtUtc,
            CreatedAtUtc = now,
        };
}
