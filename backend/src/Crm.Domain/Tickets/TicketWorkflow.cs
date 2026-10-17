namespace Crm.Domain.Tickets;

/// <summary>TICK-002 transition matrix — the single source of truth. Every pair not listed is illegal.</summary>
public static class TicketWorkflow
{
    public static bool IsAllowed(TicketStatus from, TicketStatus to) => (from, to) switch
    {
        (TicketStatus.Open, TicketStatus.InProgress) => true,     // begin handling
        (TicketStatus.InProgress, TicketStatus.Resolved) => true, // work completed
        (TicketStatus.Resolved, TicketStatus.Closed) => true,     // finish
        (TicketStatus.Resolved, TicketStatus.InProgress) => true, // resolution unsuccessful; resume
        (TicketStatus.Closed, TicketStatus.Open) => true,         // reopen for a new work cycle
        _ => false,
    };

    /// <summary>Manual escalation is only for active work.</summary>
    public static bool CanEscalate(TicketStatus status) => status is TicketStatus.Open or TicketStatus.InProgress;
}
