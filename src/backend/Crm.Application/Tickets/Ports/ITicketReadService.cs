using Crm.Application.Tickets.Contracts;
namespace Crm.Application.Tickets.Ports;

public interface ITicketReadService
{
    Task<TicketDetailDto?> GetAsync(string reference, CancellationToken cancellationToken);
    Task<TicketPageDto<TicketHistoryDto>?> GetHistoryAsync(string reference, int page, int pageSize, CancellationToken cancellationToken);
}
