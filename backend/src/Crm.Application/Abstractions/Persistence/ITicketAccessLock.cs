namespace Crm.Application.Abstractions.Persistence;

/// <summary>A ticket's current assignment, read under a row lock that is held until the transaction ends.</summary>
public sealed record TicketAssignmentLock(Guid TicketId, Guid? AssignedAgentUserId);

/// <summary>
/// Write-time authorization (DASH-002 F3). Both methods MUST run inside an explicit transaction (an ITransactionalCommand);
/// they throw InvalidOperationException otherwise. UPDLOCK + HOLDLOCK + ROWLOCK keep the returned facts true until commit:
/// a concurrent ticket (re/un)assignment or membership removal/insert of the same key waits. Reads only — changes nothing.
/// </summary>
public interface ITicketAccessLock
{
    /// <summary>Null when the ticket does not exist.</summary>
    Task<TicketAssignmentLock?> LockTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    /// <summary>True when agentUserId has an active membership on ticketId. HOLDLOCK also locks the key range when absent,
    /// so a concurrent invite of the same Agent waits.</summary>
    Task<bool> LockActiveMembershipAsync(Guid ticketId, Guid agentUserId, CancellationToken cancellationToken = default);
}
