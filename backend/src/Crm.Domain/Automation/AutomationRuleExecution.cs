using Crm.Domain.Sla;

namespace Crm.Domain.Automation;

/// <summary>
/// Append-only processing record. Unique (RuleId, TicketId, SlaKind) — the dedup key for every automated effect (S8).
/// Inserted in the same save/transaction as its effect. SlaKind is null for assignment.
/// </summary>
public sealed class AutomationRuleExecution
{
    private AutomationRuleExecution() { } // EF Core

    public long Id { get; private set; }
    public Guid RuleId { get; private set; }
    public Guid TicketId { get; private set; }
    public SlaKind? SlaKind { get; private set; }
    public AutomationTrigger Trigger { get; private set; }
    public AutomationAction Action { get; private set; }
    public AutomationOutcome Outcome { get; private set; }
    public string? Detail { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static AutomationRuleExecution Create(
        Guid ruleId, Guid ticketId, SlaKind? kind, AutomationTrigger trigger, AutomationAction action,
        AutomationOutcome outcome, string? detail, DateTimeOffset now) =>
        new()
        {
            RuleId = ruleId,
            TicketId = ticketId,
            SlaKind = kind,
            Trigger = trigger,
            Action = action,
            Outcome = outcome,
            Detail = detail is { Length: > 300 } ? detail[..300] : detail,
            OccurredAtUtc = now,
        };
}
