using Crm.Domain.Tickets;

namespace Crm.Domain.Sla;

/// <summary>One business-minute target pair for a ticket priority. Owned by SlaPolicy.</summary>
public sealed record SlaTarget(TicketPriority Priority, int FirstResponseMinutes, int ResolutionMinutes);
