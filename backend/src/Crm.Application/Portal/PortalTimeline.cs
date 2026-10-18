using Crm.Domain.Tickets;

namespace Crm.Application.Portal;

internal static class PortalTimeline
{
    /// <summary>
    /// P5: Created ⇒ Created entry (recorded); StatusChanged with parseable names ⇒ StatusChanged. Anything else is ignored.
    /// No Created row (pre-TICK-002 ticket) ⇒ one Created entry from Tickets.CreatedAtUtc, IsRecorded false. Never invents transitions.
    /// Input is already oldest first (OccurredAtUtc, Id); the synthesized entry goes first (neutral wording: staff may have created the ticket).
    /// </summary>
    public static IReadOnlyList<PortalTimelineEntry> Build(PortalTicketHistory history)
    {
        var entries = new List<PortalTimelineEntry>();
        foreach (var e in history.Events)
        {
            if (e.Type == TicketActivityType.Created)
            {
                entries.Add(new(PortalTimelineEntryKind.Created, null, TicketStatus.Open, e.OccurredAtUtc, IsRecorded: true));
            }
            else if (e.Type == TicketActivityType.StatusChanged
                && Enum.TryParse<TicketStatus>(e.OldValue, out var from) && Enum.TryParse<TicketStatus>(e.NewValue, out var to))
            {
                entries.Add(new(PortalTimelineEntryKind.StatusChanged, from, to, e.OccurredAtUtc, IsRecorded: true));
            }
        }

        if (!history.Events.Any(e => e.Type == TicketActivityType.Created))
        {
            entries.Insert(0, new(PortalTimelineEntryKind.Created, null, TicketStatus.Open, history.TicketCreatedAtUtc, IsRecorded: false));
        }

        return entries;
    }
}
