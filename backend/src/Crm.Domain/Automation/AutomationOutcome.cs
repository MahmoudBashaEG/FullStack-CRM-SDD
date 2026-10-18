namespace Crm.Domain.Automation;

/// <summary>What a rule execution did. Stored by name.</summary>
public enum AutomationOutcome
{
    Assigned = 0,
    Notified = 1,
    Escalated = 2,
    AlreadyEscalated = 3,
    NotEscalatable = 4,
}
