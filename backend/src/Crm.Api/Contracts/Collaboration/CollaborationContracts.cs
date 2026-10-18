using Crm.Application.Collaboration;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Collaboration;

/// <summary>Minimal ticket context: no description, customer or activity data.</summary>
public sealed record CollaborationTicketResponse(
    Guid TicketId, string Subject, TicketStatus Status, TicketPriority Priority, string? AssignedAgentName,
    CollaborationAccess Access, bool CanManage)
{
    public static CollaborationTicketResponse From(CollaborationTicketContext c) =>
        new(c.TicketId, c.Subject, c.Status, c.Priority, c.AssignedAgentName, c.Access, c.CanManage);
}

public sealed record TicketMessageResponse(long Id, Guid AuthorUserId, string AuthorName, string Body, DateTimeOffset CreatedAtUtc)
{
    public static TicketMessageResponse From(TicketMessageDetails m) => new(m.Id, m.AuthorUserId, m.AuthorName, m.Body, m.CreatedAtUtc);
}

public sealed record PostTicketMessageRequest(string Body);

public sealed record TicketCollaboratorResponse(Guid AgentUserId, string AgentName, bool IsActive, string AddedByName, DateTimeOffset AddedAtUtc)
{
    public static TicketCollaboratorResponse From(TicketCollaboratorDetails c) =>
        new(c.AgentUserId, c.AgentName, c.IsActive, c.AddedByName, c.AddedAtUtc);
}

public sealed record InviteCollaboratorRequest(Guid AgentUserId);

public sealed record CollaboratorCandidateResponse(Guid AgentUserId, string AgentName)
{
    public static CollaboratorCandidateResponse From(CollaboratorCandidate c) => new(c.AgentUserId, c.AgentName);
}

public sealed record MyCollaborationResponse(
    Guid TicketId, string Subject, TicketStatus Status, TicketPriority Priority,
    bool IsSuspended, bool IsPrimaryAssignee, DateTimeOffset AddedAtUtc, DateTimeOffset? LastMessageAtUtc)
{
    public static MyCollaborationResponse From(MyCollaborationDetails c) =>
        new(c.TicketId, c.Subject, c.Status, c.Priority, c.IsSuspended, c.IsPrimaryAssignee, c.AddedAtUtc, c.LastMessageAtUtc);
}
