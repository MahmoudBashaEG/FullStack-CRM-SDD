namespace Crm.Domain.Tickets;

/// <summary>Fixed set for TICK-001 (no administration). Stored as the member name.</summary>
public enum TicketCategory
{
    General = 0,
    Technical = 1,
    Billing = 2,
    Other = 3,
}
