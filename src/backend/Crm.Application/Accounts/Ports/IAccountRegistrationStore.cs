using Crm.Application.Accounts.Contracts;
using Crm.Application.Common;
namespace Crm.Application.Accounts.Ports;

public interface IAccountRegistrationStore
{
    Task<OperationResult<AccountIdentity>> RegisterAsync(NewAccount account, CancellationToken cancellationToken);
}
