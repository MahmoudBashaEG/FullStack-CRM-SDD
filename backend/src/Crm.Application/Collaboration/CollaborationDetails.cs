using Crm.Domain.Tickets;

namespace Crm.Application.Collaboration;

public enum CollaborationAccess { SuperAdmin, PrimaryAgent, Collaborator }

/// <summary>Minimal ticket context for the collaboration surface. Deliberately no description, customer or activity data.</summary>
public sealed record CollaborationTicketContext(
    Guid TicketId, string Subject, TicketStatus Status, TicketPriority Priority,
    Guid? AssignedAgentUserId, string? AssignedAgentName, CollaborationAccess Access)
{
    public bool CanManage => Access is CollaborationAccess.SuperAdmin or CollaborationAccess.PrimaryAgent;
}

public sealed record TicketMessageDetails(long Id, Guid TicketId, Guid AuthorUserId, string AuthorName, string Body, DateTimeOffset CreatedAtUtc);

public sealed record TicketCollaboratorDetails(
    Guid AgentUserId, string AgentName, bool IsActive, string AddedByName, DateTimeOffset AddedAtUtc);

public sealed record CollaboratorCandidate(Guid AgentUserId, string AgentName);

/// <summary>A ticket the Agent is an active collaborator on. IsSuspended = ticket currently unassigned (no access).
/// IsPrimaryAssignee = the Agent now holds the ticket (use Ticket Details instead).</summary>
public sealed record MyCollaborationDetails(
    Guid TicketId, string Subject, TicketStatus Status, TicketPriority Priority,
    bool IsSuspended, bool IsPrimaryAssignee, DateTimeOffset AddedAtUtc, DateTimeOffset? LastMessageAtUtc);
