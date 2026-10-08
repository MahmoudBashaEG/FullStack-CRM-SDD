using Crm.Application.Tickets.Contracts;

namespace Crm.Application.Tickets.Ports;

public interface ITicketReferenceData
{
    Task<IReadOnlyList<ReferenceChoice>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferenceChoice>> GetAgentsAsync(CancellationToken cancellationToken);
}
