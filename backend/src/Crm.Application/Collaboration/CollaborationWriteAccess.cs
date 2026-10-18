using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;

namespace Crm.Application.Collaboration;

internal static class CollaborationWriteAccess
{
    /// <summary>Null = no access (handler throws 404). Locks the ticket row, and for a non-primary Agent the membership key, until commit.</summary>
    public static async Task<(CollaborationAccess Access, TicketAssignmentLock Ticket)?> ResolveAsync(
        ITicketAccessLock locks, StaffActor actor, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await locks.LockTicketAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        if (actor.IsSuperAdmin)
        {
            return (CollaborationAccess.SuperAdmin, ticket);
        }

        if (ticket.AssignedAgentUserId == actor.UserId)
        {
            return (CollaborationAccess.PrimaryAgent, ticket);
        }

        // Unassigned ⇒ suspended, without touching the membership row.
        return ticket.AssignedAgentUserId is not null && await locks.LockActiveMembershipAsync(ticketId, actor.UserId, cancellationToken)
            ? (CollaborationAccess.Collaborator, ticket)
            : null;
    }
}
