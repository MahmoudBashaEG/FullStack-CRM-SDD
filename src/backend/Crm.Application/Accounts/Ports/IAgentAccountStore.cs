using Crm.Application.Accounts.Contracts;
using Crm.Application.Common;
namespace Crm.Application.Accounts.Ports;
public interface IAgentAccountStore
{
    Task<OperationResult<AgentAccountDto>> CreateAsync(NewAccount account,CancellationToken ct);
    Task<OperationResult<AgentAccountDto>> GetAsync(Guid id,CancellationToken ct);
    Task<AgentPage> ListAsync(int page,int pageSize,CancellationToken ct);
    Task<OperationResult<AgentAccountDto>> RenameAsync(Guid id,string name,byte[] version,CancellationToken ct);
    Task<OperationResult<AgentAccountDto>> SetAvailabilityAsync(Guid id,bool active,byte[] version,CancellationToken ct);
}
