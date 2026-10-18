namespace Crm.Domain.Tickets;

/// <summary>Ticket history event kinds. Stored by name.</summary>
public enum TicketActivityType
{
    Created = 0,
    FieldChanged = 1,
    AssigneeChanged = 2,
    StatusChanged = 3,
    Escalated = 4,
    EscalationCleared = 5,
    EscalationAutoCleared = 6,
    FirstResponseRecorded = 7,
}
