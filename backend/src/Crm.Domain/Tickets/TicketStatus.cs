namespace Crm.Domain.Tickets;

/// <summary>
/// System-managed. TICK-001 only creates Open tickets and has no transitions;
/// TICK-002 appends the other lifecycle states (stored as names, so appending is additive).
/// </summary>
public enum TicketStatus
{
    Open = 0,
}
