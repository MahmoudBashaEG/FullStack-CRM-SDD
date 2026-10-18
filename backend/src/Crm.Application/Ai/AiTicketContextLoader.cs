using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;

namespace Crm.Application.Ai;

public sealed record AiTicketContext(TicketDetails Ticket, IReadOnlyList<TicketActivityDetails> Activity);

/// <summary>Re-reads the ticket under the caller's CURRENT scope on every AI call (AI-001 §4). Out of scope ⇒ 404, never client data.</summary>
public sealed class AiTicketContextLoader(ITicketRepository tickets, AiLimits limits)
{
    public async Task<TicketDetails> GetTicketAsync(StaffActor actor, Guid ticketId, CancellationToken cancellationToken) =>
        await tickets.FindDetailsByIdAsync(ticketId, actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{ticketId}' was not found.");

    public async Task<AiTicketContext> LoadAsync(StaffActor actor, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await GetTicketAsync(actor, ticketId, cancellationToken);
        if (limits.ActivityEntries == 0)
        {
            return new AiTicketContext(ticket, []);
        }

        // Null = reassigned between the two reads: same 404, no partial context.
        var activity = await tickets.ListActivityAsync(ticketId, actor.AgentScope, 1, limits.ActivityEntries, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{ticketId}' was not found.");
        return new AiTicketContext(ticket, activity.Items);
    }
}
