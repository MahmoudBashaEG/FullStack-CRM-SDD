namespace Crm.Domain.Automation;

/// <summary>Fixed rule triggers. Stored by name.</summary>
public enum AutomationTrigger
{
    TicketCreated = 0,
    SlaWarningDue = 1,
    SlaBreached = 2,
}
