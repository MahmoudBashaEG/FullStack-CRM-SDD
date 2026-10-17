using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Tickets;

// No status, creator or timestamps. assignedAgentUserId: SuperAdmin only (active Agent or null);
// Agents must omit it (their tickets are assigned to them). priority omitted = Medium.
public sealed record CreateTicketRequest(
    Guid CustomerId, string Subject, string Description, TicketCategory? Category, TicketPriority? Priority, Guid? AssignedAgentUserId);
