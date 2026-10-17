namespace Crm.Api.Contracts.Tickets;

/// <summary>SuperAdmin only. null = unassigned; otherwise an active Agent's user id.</summary>
public sealed record ChangeTicketAssigneeRequest(Guid? AssignedAgentUserId);
