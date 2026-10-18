using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.AgentTasks;
using Crm.Domain.Identity;
using FluentValidation;
using FluentValidation.Results;

namespace Crm.Application.AgentTasks;

internal static class AgentTaskGuards
{
    public static ValidationException Invalid(string property, string message) => new([new ValidationFailure(property, message)]);

    public static async Task EnsureActiveAgentAsync(IIdentityService identity, Guid userId, string property, CancellationToken ct)
    {
        var user = await identity.FindByIdAsync(userId, ct);
        if (user is not { Role: UserRoles.Agent, IsActive: true })
        {
            throw Invalid(property, "Select an active Agent.");
        }
    }

    /// <summary>F1: the If-Match version must equal the loaded row. Checked before any rule, also for no-ops.</summary>
    public static void EnsureVersion(AgentTask task, byte[] expectedVersion)
    {
        if (!task.Version.AsSpan().SequenceEqual(expectedVersion))
        {
            throw new ConflictException("The task was changed by someone else. Reload it and try again.");
        }
    }

    /// <summary>
    /// F3 — call only inside an ITransactionalCommand. The ticket row stays locked until commit, so a concurrent reassignment
    /// either committed before (⇒ rejected here) or waits for this transaction. Collaborator membership never counts.
    /// One generic message for missing / unassigned / someone else's ticket (no existence leak).
    /// </summary>
    public static async Task EnsureTicketAssignedToOwnerAsync(ITicketAccessLock ticketLock, Guid ticketId, Guid ownerUserId, CancellationToken ct)
    {
        var ticket = await ticketLock.LockTicketAsync(ticketId, ct);
        if (ticket?.AssignedAgentUserId != ownerUserId)
        {
            throw Invalid("ticketId", "Select a ticket that is currently assigned to the task owner.");
        }
    }

    /// <summary>Only for a new or changed reminder time.</summary>
    public static void EnsureValidReminder(DateTimeOffset remindAtUtc, DateTimeOffset? dueAtUtc, DateTimeOffset now)
    {
        if (remindAtUtc <= now)
        {
            throw Invalid("remindAtUtc", "Choose a future reminder time.");
        }

        if (dueAtUtc is { } due && remindAtUtc > due)
        {
            throw Invalid("remindAtUtc", "The reminder must be at or before the due time.");
        }
    }

    public static void EnsureNotArchived(AgentTask task)
    {
        if (task.IsArchived)
        {
            throw new ConflictException("The task is archived. Restore it first.");
        }
    }
}
