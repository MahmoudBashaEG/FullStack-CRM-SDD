namespace Crm.Domain.Tickets;

/// <summary>Fixed set, chosen by staff (never calculated). Medium is the default on creation.</summary>
public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3,
}
