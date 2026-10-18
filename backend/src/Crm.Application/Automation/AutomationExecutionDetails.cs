using Crm.Domain.Automation;
using Crm.Domain.Sla;

namespace Crm.Application.Automation;

public sealed record AutomationExecutionDetails(
    long Id,
    Guid RuleId,
    string RuleName,
    Guid TicketId,
    string TicketSubject,
    SlaKind? SlaKind,
    AutomationTrigger Trigger,
    AutomationAction Action,
    AutomationOutcome Outcome,
    string? Detail,
    DateTimeOffset OccurredAtUtc);
