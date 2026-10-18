using Crm.Domain.Tickets;

namespace Crm.Application.Sla;

/// <summary>
/// SLA-001 hook run by CreateTicketCommandHandler after Ticket.Create and before the single save.
/// Stages the SLA snapshot (S1) and at most one rule-based assignment (S5) on the same unit of work.
/// Future ticket channels (web form, email) reuse this hook.
/// </summary>
public interface ITicketCreationAutomation
{
    Task ApplyAsync(Ticket ticket, CancellationToken cancellationToken);
}
