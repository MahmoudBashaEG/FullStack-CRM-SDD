using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Tickets;

// The only editable fields in TICK-001.
public sealed record UpdateTicketRequest(string Subject, string Description, TicketCategory? Category, TicketPriority? Priority);
