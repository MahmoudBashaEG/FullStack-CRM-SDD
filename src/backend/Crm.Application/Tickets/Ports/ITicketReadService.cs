using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Queries;
namespace Crm.Application.Tickets.Ports;

public interface ITicketReadService
{
    Task<TicketPageDto<TicketSummaryDto>> ListAsync(TicketListCriteria criteria, CancellationToken cancellationToken);
    Task<TicketDetailDto?> GetAsync(string reference, CancellationToken cancellationToken);
    Task<TicketPageDto<TicketHistoryDto>?> GetHistoryAsync(string reference, int page, int pageSize, CancellationToken cancellationToken);
}
