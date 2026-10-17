namespace Crm.Domain.Tickets;

/// <summary>
/// System-managed lifecycle, changed only through Ticket.TransitionTo (rules in TicketWorkflow).
/// Stored as names, so the TICK-002 members were appended without remapping existing rows.
/// </summary>
public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3,
}
