using Crm.Application.Tickets.Contracts;
using Crm.Domain.Tickets;
namespace Crm.Application.Tickets.Ports;

public sealed record TicketMutationContext(Ticket Ticket, TicketCategoryDto Category, TicketAgentDto? Agent,
    Func<Guid, CancellationToken, Task<TicketCategoryDto?>> SelectCategory,
    Func<Guid, CancellationToken, Task<TicketAgentDto?>> SelectAgent);
