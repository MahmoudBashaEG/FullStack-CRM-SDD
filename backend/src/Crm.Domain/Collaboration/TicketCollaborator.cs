namespace Crm.Domain.Collaboration;

/// <summary>
/// Explicit invitation of an Agent to one ticket's internal discussion (DASH-002 D3). Grants ONLY the collaboration surface,
/// never ticket details/activity/actions. Removal is recorded (RemovedAtUtc/By), never deleted; re-inviting adds a new row.
/// </summary>
public sealed class TicketCollaborator
{
    private TicketCollaborator() { } // EF Core

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AgentUserId { get; private set; }
    public Guid AddedByUserId { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }
    public Guid? RemovedByUserId { get; private set; }
    public DateTimeOffset? RemovedAtUtc { get; private set; }

    public static TicketCollaborator Invite(Guid id, Guid ticketId, Guid agentUserId, Guid addedByUserId, DateTimeOffset now) =>
        new() { Id = id, TicketId = ticketId, AgentUserId = agentUserId, AddedByUserId = addedByUserId, AddedAtUtc = now };

    public void Remove(Guid removedByUserId, DateTimeOffset now)
    {
        RemovedByUserId = removedByUserId;
        RemovedAtUtc = now;
    }
}
