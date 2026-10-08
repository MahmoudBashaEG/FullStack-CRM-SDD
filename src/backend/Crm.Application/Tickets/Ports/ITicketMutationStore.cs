using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Domain.Tickets;
namespace Crm.Application.Tickets.Ports;

public interface ITicketMutationStore
{
    Task<OperationResult<TicketDetailDto>> CreateAsync(Guid categoryId, Func<TicketCategoryDto?, OperationResult<Ticket>> create, CancellationToken cancellationToken);
}
