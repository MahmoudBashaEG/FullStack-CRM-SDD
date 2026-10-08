using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Domain.Tickets;
namespace Crm.Application.Tickets.Ports;

public interface ITicketMutationStore
{
    Task<OperationResult<TicketDetailDto>> MutateAsync(string reference, byte[] expectedVersion, Func<TicketMutationContext, CancellationToken, Task<Failure?>> mutation, CancellationToken cancellationToken);
    Task<OperationResult<TicketDetailDto>> CreateAsync(Guid categoryId, Func<TicketCategoryDto?, OperationResult<Ticket>> create, CancellationToken cancellationToken);
}
